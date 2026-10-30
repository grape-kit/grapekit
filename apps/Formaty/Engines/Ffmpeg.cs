using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;

namespace Formaty.Engines;

/// <summary>
/// FFmpeg/FFprobe çağrıları (ayrı işlem: çökme uygulamayı etkilemez). İlerleme "-progress pipe:1" ile okunur,
/// iptal işlem ağacını öldürür, duraklatma işlemi askıya alır.
/// </summary>
public static class Ffmpeg
{
    static readonly string Dir = Path.Combine(AppContext.BaseDirectory, "engines");
    public static string Exe => Path.Combine(Dir, "ffmpeg.exe");
    public static string Probe => Path.Combine(Dir, "ffprobe.exe");
    public static bool Available => File.Exists(Exe) && File.Exists(Probe);
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // ---------------- Bilgi ----------------
    public sealed record MediaInfo(double Duration, bool HasVideo, bool HasAudio, int Width, int Height, double Fps, int Rotation,
        string VideoCodec, string AudioCodec, int Channels, int SampleRate, long Bitrate, string Format);

    public static async Task<MediaInfo> InfoAsync(string path, CancellationToken ct = default)
    {
        var (code, output, _) = await RunCaptureAsync(Probe, ["-v", "error", "-print_format", "json", "-show_format", "-show_streams", path], ct);
        if (code != 0 || string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException(Text.T("err.unreadable"));
        var j = JsonNode.Parse(output)!;
        var streams = j["streams"]?.AsArray() ?? [];
        // Kapak görseli (attached_pic) gerçek video sayılmaz
        var v = streams.FirstOrDefault(s => s?["codec_type"]?.GetValue<string>() == "video" && (s?["disposition"]?["attached_pic"]?.GetValue<int>() ?? 0) == 0);
        var a = streams.FirstOrDefault(s => s?["codec_type"]?.GetValue<string>() == "audio");
        double dur = ParseD(j["format"]?["duration"]) ?? ParseD(v?["duration"]) ?? ParseD(a?["duration"]) ?? 0;
        int rot = 0;
        if (v?["side_data_list"] is JsonArray sd) foreach (var x in sd) if (x?["rotation"] is JsonValue r && r.TryGetValue<int>(out var rr)) rot = rr;
        if (v?["tags"]?["rotate"] is JsonValue tr && int.TryParse(tr.GetValue<string>(), out var t2)) rot = t2;
        double fps = 0;
        if (v?["avg_frame_rate"]?.GetValue<string>() is { } fr && fr.Split('/') is [var n, var d] && double.TryParse(n, Inv, out var nn) && double.TryParse(d, Inv, out var dd) && dd > 0) fps = nn / dd;
        return new MediaInfo(dur, v != null, a != null,
            v?["width"]?.GetValue<int>() ?? 0, v?["height"]?.GetValue<int>() ?? 0, fps, rot,
            v?["codec_name"]?.GetValue<string>() ?? "", a?["codec_name"]?.GetValue<string>() ?? "",
            a?["channels"]?.GetValue<int>() ?? 0, int.TryParse(a?["sample_rate"]?.GetValue<string>(), out var sr) ? sr : 0,
            long.TryParse(j["format"]?["bit_rate"]?.GetValue<string>(), out var br) ? br : 0,
            j["format"]?["format_name"]?.GetValue<string>() ?? "");
    }

    static double? ParseD(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) && double.TryParse(s, Inv, out var d) ? d : null;

    // ---------------- Donanım kodlayıcıları ----------------
    static readonly SemaphoreSlim HwLock = new(1, 1);
    static Dictionary<string, bool>? _hw;

    /// <summary>Kodlayıcının bu bilgisayarda gerçekten çalışıp çalışmadığı (sürücü/donanım yoksa hata verir). Sonuç önbelleğe alınır.</summary>
    public static async Task<bool> WorksAsync(string encoder)
    {
        await HwLock.WaitAsync();
        try
        {
            _hw ??= [];
            if (_hw.TryGetValue(encoder, out var ok)) return ok;
            var pix = encoder.EndsWith("_qsv") ? "nv12" : "yuv420p";
            var (code, _, _) = await RunCaptureAsync(Exe, ["-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i", "color=black:s=320x240:r=30:d=0.2",
                "-pix_fmt", pix, "-c:v", encoder, "-frames:v", "4", "-f", "null", "-"], CancellationToken.None, TimeSpan.FromSeconds(15));
            _hw[encoder] = code == 0;
            return code == 0;
        }
        finally { HwLock.Release(); }
    }

    /// <summary>Codec için kullanılacak kodlayıcı: donanım (NVIDIA → Intel → AMD → Windows), yoksa yazılım.</summary>
    public static async Task<string> PickEncoderAsync(string codec, bool allowHw)
    {
        string[] hw = codec switch
        {
            "h264" => ["h264_nvenc", "h264_qsv", "h264_amf", "h264_mf"],
            "hevc" => ["hevc_nvenc", "hevc_qsv", "hevc_amf", "hevc_mf"],
            "av1" => ["av1_nvenc", "av1_qsv", "av1_amf"],
            _ => [],
        };
        if (allowHw) foreach (var e in hw) if (await WorksAsync(e)) return e;
        return codec switch
        {
            "h264" => "libopenh264",
            "hevc" => await WorksAsync("hevc_mf") ? "hevc_mf" : "libkvazaar",
            "av1" => "libsvtav1",
            "vp9" => "libvpx-vp9",
            "vp8" => "libvpx",
            _ => "mpeg4",
        };
    }

    public static async Task<JsonArray> HardwareListAsync()
    {
        var list = new JsonArray();
        foreach (var e in new[] { "h264_nvenc", "hevc_nvenc", "av1_nvenc", "h264_qsv", "hevc_qsv", "av1_qsv", "h264_amf", "hevc_amf", "av1_amf", "h264_mf", "hevc_mf" })
            if (await WorksAsync(e)) list.Add(e);
        return list;
    }

    // ---------------- Çalıştırma ----------------
    /// <summary>Bir işi çalıştırır. progress: 0..1 (süre bilinmiyorsa -1).</summary>
    public static async Task RunAsync(IReadOnlyList<string> args, double duration, Action<double>? progress, CancellationToken ct, PauseToken? pause = null)
    {
        var psi = new ProcessStartInfo(Exe)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in new[] { "-hide_banner", "-nostdin", "-y", "-loglevel", "error", "-progress", "pipe:1", "-nostats" }) psi.ArgumentList.Add(a);
        foreach (var a in args) psi.ArgumentList.Add(a);
        if (Environment.GetEnvironmentVariable("FORMATY_TRACE") == "1") Console.WriteLine("    ffmpeg " + string.Join(' ', args.Select(x => x.Contains(' ') ? $"\"{x}\"" : x)));
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("ffmpeg");
        TryLowerPriority(p);
        var err = new StringBuilder();
        var errTask = Task.Run(async () =>
        {
            string? line;
            while ((line = await p.StandardError.ReadLineAsync()) != null) lock (err) { if (err.Length < 8000) err.AppendLine(line); }
        });
        var outTask = Task.Run(async () =>
        {
            string? line;
            while ((line = await p.StandardOutput.ReadLineAsync()) != null)
            {
                if (progress == null) continue;
                if (line.StartsWith("out_time_us=") || line.StartsWith("out_time_ms="))
                {
                    if (long.TryParse(line.AsSpan(line.IndexOf('=') + 1), out var us) && duration > 0) progress(Math.Clamp(us / 1e6 / duration, 0, 1));
                }
                else if (line == "progress=end") progress(1);
            }
        });
        using var reg = ct.Register(() => { try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch { } });
        using var pauseReg = pause?.Watch(paused => { if (paused) Suspend(p); else Resume(p); });
        await p.WaitForExitAsync(CancellationToken.None);
        await Task.WhenAll(errTask, outTask);
        ct.ThrowIfCancellationRequested();
        if (p.ExitCode != 0) throw new InvalidOperationException(CleanError(err.ToString()));
    }

    static void TryLowerPriority(Process p) { try { p.PriorityClass = ProcessPriorityClass.BelowNormal; } catch { } }

    /// <summary>FFmpeg hata çıktısından kullanıcıya gösterilecek kısa satır.</summary>
    static string CleanError(string s)
    {
        var lines = s.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => !l.StartsWith("Conversion failed")).ToList();
        var last = lines.LastOrDefault() ?? Text.T("err.convert");
        return last.Length > 300 ? last[..300] : last;
    }

    public static async Task<(int Code, string Out, string Err)> RunCaptureAsync(string exe, IEnumerable<string> args, CancellationToken ct, TimeSpan? timeout = null)
    {
        var psi = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var o = p.StandardOutput.ReadToEndAsync(ct);
        var e = p.StandardError.ReadToEndAsync(ct);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (timeout is { } t) cts.CancelAfter(t);
        try { await p.WaitForExitAsync(cts.Token); }
        catch (OperationCanceledException) { try { p.Kill(true); } catch { } return (-1, "", "timeout"); }
        return (p.ExitCode, await o, await e);
    }

    // ---------------- Duraklatma (işlemi askıya al) ----------------
    [DllImport("ntdll.dll")] static extern int NtSuspendProcess(IntPtr h);
    [DllImport("ntdll.dll")] static extern int NtResumeProcess(IntPtr h);
    static void Suspend(Process p) { try { if (!p.HasExited) NtSuspendProcess(p.Handle); } catch { } }
    static void Resume(Process p) { try { if (!p.HasExited) NtResumeProcess(p.Handle); } catch { } }

    public static string F(double d) => d.ToString("0.###", Inv);
}

/// <summary>İşi duraklatma bayrağı: bekleyenler beklemeye alınır, çalışan FFmpeg askıya alınır.</summary>
public sealed class PauseToken
{
    readonly ManualResetEventSlim _gate = new(true);
    event Action<bool>? Changed;
    public bool Paused => !_gate.IsSet;
    public void Set(bool paused) { if (paused) _gate.Reset(); else _gate.Set(); Changed?.Invoke(paused); }
    public Task WaitAsync(CancellationToken ct) => Task.Run(() => _gate.Wait(ct), ct);
    public IDisposable Watch(Action<bool> fn) { Changed += fn; if (Paused) fn(true); return new Unsub(() => Changed -= fn); }
    sealed class Unsub(Action a) : IDisposable { public void Dispose() => a(); }
}
