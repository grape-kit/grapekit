using System.Diagnostics;
using System.Text.Json.Nodes;
using Formaty.Engines;

namespace Formaty;

/// <summary>
/// Motor denemesi (geliştirme ve kurulum doğrulaması): Formaty.exe --selftest &lt;klasör&gt;
/// Klasördeki örnekleri (yoksa FFmpeg ile üretir) her hedef biçime çevirir, sonucu ve süreyi yazar.
/// </summary>
internal static class SelfTest
{
    public static int Run(string dir)
    {
        AllocConsole();
        Directory.CreateDirectory(dir);
        var outDir = Path.Combine(dir, "out");
        if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        Directory.CreateDirectory(outDir);
        var temp = Path.Combine(Path.GetTempPath(), "Formaty", "selftest");
        Directory.CreateDirectory(temp);
        int fail = 0;
        void check(string name, Func<Task> act)
        {
            if (Environment.GetEnvironmentVariable("FORMATY_ONLY") is { Length: > 0 } f && !name.Contains(f, StringComparison.OrdinalIgnoreCase) && !name.StartsWith("örnek")) return;
            var sw = Stopwatch.StartNew();
            try { act().GetAwaiter().GetResult(); Console.WriteLine($"  tamam  {name,-34} {sw.ElapsedMilliseconds,6} ms"); }
            catch (Exception ex) { fail++; Console.WriteLine($"  HATA   {name,-34} {ex.GetType().Name}: {ex.Message.Split('\n')[0]}"); }
        }
        long size(string f) => File.Exists(f) ? new FileInfo(f).Length : -1;
        var ct = CancellationToken.None;
        var pause = new PauseToken();
        var only = Environment.GetEnvironmentVariable("FORMATY_ONLY");
        Console.WriteLine($"Formaty {GrapekitApp.VersionText} motor denemesi — FFmpeg: {(Ffmpeg.Available ? "var" : "YOK")}");

        // Örnekler
        var video = Path.Combine(dir, "ornek.mp4");
        var audio = Path.Combine(dir, "ornek.wav");
        var photo = Path.Combine(dir, "ornek.jpg");
        var png = Path.Combine(dir, "ornek.png");
        if (!File.Exists(video)) check("örnek video üret", () => Ffmpeg.RunAsync(["-f", "lavfi", "-i", "testsrc2=s=1280x720:r=30:d=6", "-f", "lavfi", "-i", "sine=f=440:d=6", "-c:v", "libopenh264", "-b:v", "2M", "-c:a", "aac", "-shortest", video], 6, null, ct));
        if (!File.Exists(audio)) check("örnek ses üret", () => Ffmpeg.RunAsync(["-f", "lavfi", "-i", "sine=f=330:d=8", "-ac", "2", audio], 8, null, ct));
        if (!File.Exists(photo)) check("örnek fotoğraf üret", () => Ffmpeg.RunAsync(["-f", "lavfi", "-i", "testsrc2=s=2400x1600", "-frames:v", "1", "-q:v", "2", photo], 0, null, ct));
        if (!File.Exists(png)) check("örnek png üret", () => Ffmpeg.RunAsync(["-f", "lavfi", "-i", "color=c=0xFF5A3F@0.5:s=512x512,format=rgba", "-frames:v", "1", png], 0, null, ct));

        Console.WriteLine("\nFotoğraf:");
        foreach (var f in Images.Targets)
        {
            var dst = Path.Combine(outDir, "foto." + Formats.OutExt(f));
            var o = new Opt(new JsonObject { ["quality"] = 80, ["resize"] = new JsonObject { ["mode"] = "long", ["long"] = 1600 } });
            check($"jpg → {f}", () => f == "pdf" ? PdfOps.ImagesToPdfAsync([photo], dst, o, temp, _ => { }, ct) : Images.ConvertAsync(photo, dst, f, o, temp, ct));
        }
        check("png (saydam) → ico", () => Images.ConvertAsync(png, Path.Combine(outDir, "simge.ico"), "ico", new Opt(null), temp, ct));
        check("jpg → webp ≤ 150 KB", async () =>
        {
            var dst = Path.Combine(outDir, "hedef.webp");
            await Images.ConvertAsync(photo, dst, "webp", new Opt(new JsonObject { ["targetKb"] = 150 }), temp, ct);
            if (size(dst) > 150 * 1024) throw new Exception($"boyut {size(dst) / 1024} KB");
        });
        foreach (var f in new[] { "bmp", "ico", "jxl", "jp2", "avif", "tif" })
        {
            var s = Path.Combine(outDir, "foto." + f);
            if (File.Exists(s)) check($"{f} → png (geri okuma)", () => Images.ConvertAsync(s, Path.Combine(outDir, $"geri-{f}.png"), "png", new Opt(null), temp, ct));
        }
        foreach (var heic in Directory.GetFiles(dir, "*.heic"))
        {
            var name = Path.GetFileNameWithoutExtension(heic);
            check($"heic ({name}) → jpg", async () =>
            {
                var dst = Path.Combine(outDir, name + ".jpg");
                await Images.ConvertAsync(heic, dst, "jpg", new Opt(null), temp, ct);
                var (w, h, _) = Images.Header(heic);
                using var o = NetVips.Image.NewFromFile(dst);
                if (Math.Max(o.Width, o.Height) != Math.Max(w, h)) throw new Exception($"boyut {o.Width}x{o.Height}, beklenen {w}x{h}");
            });
        }
        check("filigran + 90° + kırpma", () => Images.ConvertAsync(photo, Path.Combine(outDir, "filigran.jpg"), "jpg",
            new Opt(new JsonObject { ["rotate"] = "90", ["watermark"] = new JsonObject { ["text"] = "Formaty" }, ["resize"] = new JsonObject { ["mode"] = "exact", ["width"] = 800, ["height"] = 800 } }), temp, ct));

        Console.WriteLine("\nVideo:");
        var vi = Ffmpeg.InfoAsync(video).GetAwaiter().GetResult();
        Console.WriteLine($"  bilgi: {vi.Width}x{vi.Height} {vi.Fps:0.##} fps {vi.Duration:0.##} sn, {vi.VideoCodec}/{vi.AudioCodec}");
        Console.WriteLine("  donanım: " + string.Join(", ", Ffmpeg.HardwareListAsync().GetAwaiter().GetResult().Select(x => x!.GetValue<string>())));
        foreach (var f in new[] { "mp4", "webm", "mkv", "mov", "avi", "gif", "webp-anim" })
            check($"mp4 → {f}", () => Media.ConvertVideoAsync(video, Path.Combine(outDir, "video." + Formats.OutExt(f)), f, new Opt(new JsonObject { ["height"] = 480 }), vi, _ => { }, ct, pause));
        check("mp4 → mp4 hevc", () => Media.ConvertVideoAsync(video, Path.Combine(outDir, "video-hevc.mp4"), "mp4", new Opt(new JsonObject { ["vcodec"] = "hevc" }), vi, _ => { }, ct, pause));
        check("mp4 → mp4 av1", () => Media.ConvertVideoAsync(video, Path.Combine(outDir, "video-av1.mp4"), "mp4", new Opt(new JsonObject { ["vcodec"] = "av1", ["height"] = 360 }), vi, _ => { }, ct, pause));
        check("mp4 → 1 MB hedef", async () =>
        {
            var dst = Path.Combine(outDir, "video-1mb.mp4");
            await Media.ConvertVideoAsync(video, dst, "mp4", new Opt(new JsonObject { ["targetMb"] = 1 }), vi, _ => { }, ct, pause);
            if (size(dst) > 1.15 * 1024 * 1024) throw new Exception($"boyut {size(dst) / 1024} KB");
        });
        check("kırpma 1–3 sn, sessiz, 2x", () => Media.ConvertVideoAsync(video, Path.Combine(outDir, "video-kirp.mp4"), "mp4",
            new Opt(new JsonObject { ["start"] = 1, ["end"] = 3, ["audio"] = "remove", ["speed"] = 2 }), vi, _ => { }, ct, pause));
        check("videodan kareler (2 sn)", () => Media.FramesAsync(video, Path.Combine(outDir, "kareler"), "kare", new Opt(new JsonObject { ["every"] = 2 }), vi, _ => { }, ct, pause));
        check("video birleştirme", () => Media.ConcatAsync([video, video], Path.Combine(outDir, "birlesik.mp4"), "mp4", new Opt(null), _ => { }, ct, pause));

        Console.WriteLine("\nSes:");
        var ai = Ffmpeg.InfoAsync(audio).GetAwaiter().GetResult();
        foreach (var f in Media.AudioTargets)
            check($"wav → {f}", () => Media.ConvertAudioAsync(audio, Path.Combine(outDir, "ses." + Formats.OutExt(f)), f, new Opt(new JsonObject { ["normalize"] = -16 }), ai, _ => { }, ct, pause));
        check("mp3 VBR V2 + mono + 22 kHz", () => Media.ConvertAudioAsync(audio, Path.Combine(outDir, "ses-vbr.mp3"), "mp3", new Opt(new JsonObject { ["mode"] = "vbr", ["vbr"] = 2, ["channels"] = "mono", ["sampleRate"] = 22050 }), ai, _ => { }, ct, pause));
        check("wav 24 bit", () => Media.ConvertAudioAsync(audio, Path.Combine(outDir, "ses-24.wav"), "wav", new Opt(new JsonObject { ["bits"] = 24 }), ai, _ => { }, ct, pause));
        check("videodan ses (mp3)", () => Media.ConvertAudioAsync(video, Path.Combine(outDir, "videodan.mp3"), "mp3", new Opt(null), vi, _ => { }, ct, pause));
        var withCover = Path.Combine(outDir, "kapakli.mp3");
        check("kapaklı mp3 üret", () => Ffmpeg.RunAsync(["-i", audio, "-i", photo, "-map", "0:a", "-map", "1:v", "-c:a", "libmp3lame", "-b:a", "128k", "-c:v", "mjpeg", "-vf", "scale=500:-2", "-disposition:v", "attached_pic", "-id3v2_version", "3", "-metadata", "title=Deneme", withCover], 8, null, ct));
        foreach (var f in new[] { "aac", "flac", "mp3" })
            check($"kapak korunur → {f}", async () =>
            {
                var src = await Ffmpeg.InfoAsync(withCover);
                var dst = Path.Combine(outDir, "kapak-" + f + "." + Formats.OutExt(f));
                await Media.ConvertAudioAsync(withCover, dst, f, new Opt(null), src, _ => { }, ct, pause);
                var r = await Ffmpeg.InfoAsync(dst);
                if (r.CoverCodec == "") throw new Exception("kapak yok");
            });
        check("ses birleştirme", () => Media.ConcatAsync([audio, audio], Path.Combine(outDir, "ses-birlesik.mp3"), "mp3", new Opt(null), _ => { }, ct, pause));

        Console.WriteLine("\nPDF:");
        var pdf = Path.Combine(outDir, "foto.pdf");
        var pdf2 = Path.Combine(outDir, "iki.pdf");
        check("görsellerden PDF (3 sayfa)", () => PdfOps.ImagesToPdfAsync([photo, png, photo], pdf2, new Opt(null), temp, _ => { }, ct));
        check("birleştirme", () => { PdfOps.Compose([.. PdfOps.AllPages(pdf, null), .. PdfOps.AllPages(pdf2, null)], Path.Combine(outDir, "birlesik.pdf"), null, new Opt(null), _ => { }, ct); return Task.CompletedTask; });
        check("sayfa düzeni (ters, döndür)", () => { PdfOps.Compose([new(pdf2, 2, 90), new(pdf2, 0, 0)], Path.Combine(outDir, "duzen.pdf"), null, new Opt(null), _ => { }, ct); return Task.CompletedTask; });
        check("bölme (her sayfa)", () => { PdfOps.Split(pdf2, Path.Combine(outDir, "bolum"), "iki", new Opt(null), null, _ => { }, ct); return Task.CompletedTask; });
        check("bölme (aralık 1-2,3)", () => { PdfOps.Split(pdf2, Path.Combine(outDir, "aralik"), "iki", new Opt(new JsonObject { ["mode"] = "ranges", ["ranges"] = "1-2, 3" }), null, _ => { }, ct); return Task.CompletedTask; });
        check("şifre koy", () => { PdfOps.Protect(pdf2, Path.Combine(outDir, "sifreli.pdf"), null, "1234", "", false, new Opt(null)); return Task.CompletedTask; });
        check("şifreli PDF şifresiz açılmaz", () => { try { PdfOps.PageCount(Path.Combine(outDir, "sifreli.pdf")); } catch { return Task.CompletedTask; } throw new Exception("şifresiz açıldı"); });
        check("şifre kaldır", () => { PdfOps.Protect(Path.Combine(outDir, "sifreli.pdf"), Path.Combine(outDir, "sifresiz.pdf"), "1234", "", "", true, new Opt(null)); if (PdfOps.PageCount(Path.Combine(outDir, "sifresiz.pdf")) != 3) throw new Exception("sayfa"); return Task.CompletedTask; });
        check("PDF → PNG (150 dpi)", () => PdfOps.ToImagesAsync(pdf2, Path.Combine(outDir, "sayfalar"), "iki", "png", new Opt(null), null, temp, _ => { }, ct));
        check("PDF → JPG şifreli", () => PdfOps.ToImagesAsync(Path.Combine(outDir, "sifreli.pdf"), Path.Combine(outDir, "sayfalar2"), "s", "jpg", new Opt(null), "1234", temp, _ => { }, ct));
        check("PDF küçült", () => { PdfOps.Compress(pdf2, Path.Combine(outDir, "kucuk.pdf"), new Opt(new JsonObject { ["level"] = "strong" }), null, _ => { }, ct); return Task.CompletedTask; });
        Console.WriteLine($"  küçültme: {size(pdf2) / 1024} KB → {size(Path.Combine(outDir, "kucuk.pdf")) / 1024} KB");

        Console.WriteLine("\nToplu iş (Jobs):");
        check("3 dosya → webp (alt klasör)", async () =>
        {
            var done = new TaskCompletionSource<JsonObject>();
            void h(string ev, JsonObject d) { if (ev == "job-done") done.TrySetResult(d); }
            Jobs.Event += h;
            Jobs.Start(new JsonObject
            {
                ["op"] = "convert", ["format"] = "webp", ["items"] = new JsonArray(photo, png, Path.Combine(outDir, "foto.bmp")),
                ["options"] = new JsonObject { ["quality"] = 75 }, ["output"] = new JsonObject { ["mode"] = "custom", ["dir"] = Path.Combine(outDir, "toplu") },
            });
            var r = await done.Task.WaitAsync(TimeSpan.FromMinutes(2));
            Jobs.Event -= h;
            if ((int)r["done"]! != 3) throw new Exception(r.ToJsonString());
        });

        Console.WriteLine($"\n{(fail == 0 ? "Hepsi başarılı." : fail + " hata.")} Çıktılar: {outDir}");
        return fail == 0 ? 0 : 1;
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern bool AllocConsole();
}
