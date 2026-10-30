using System.Diagnostics;
using System.Text.Json.Nodes;

namespace Formaty.Engines;

/// <summary>
/// Toplu iş kuyruğu. Her dosya için hedef yolu hesaplar, önce geçici adla yazar, bitince yerine taşır (yarım dosya kalmaz).
/// Paralel çalışır, duraklatılır, iptal edilir; her adımı <see cref="Event"/> ile arayüze bildirir.
/// </summary>
public static class Jobs
{
    public static event Action<string, JsonObject>? Event;
    static readonly Dictionary<string, Job> Running = [];

    sealed class Job
    {
        public required string Id, Op, Format;
        public required Opt O, Out;
        public required List<(string Path, string Rel)> Items;
        public int Parallel;
        public readonly CancellationTokenSource Cts = new();
        public readonly PauseToken Pause = new();
        public string Temp = "";
        public long InBytes, OutBytes;
        public int Done, Failed, Skipped;
        public readonly List<string> Outputs = [];
    }

    public static string Start(JsonObject req)
    {
        var o = new Opt(req);
        var items = (req["items"] as JsonArray ?? []).Select(x => x is JsonObject j ? (new Opt(j).S("path"), new Opt(j).S("rel")) : (x?.GetValue<string>() ?? "", ""))
            .Where(x => x.Item1.Length > 0).ToList();
        var job = new Job
        {
            Id = Guid.NewGuid().ToString("N")[..12], Op = o.S("op", "convert"), Format = o.S("format"),
            O = o.Sub("options"), Out = o.Sub("output"), Items = items, Parallel = o.I("parallel", 0),
        };
        lock (Running) Running[job.Id] = job;
        _ = Task.Run(() => RunAsync(job));
        return job.Id;
    }

    public static void Cancel(string id) { lock (Running) if (Running.TryGetValue(id, out var j)) { j.Pause.Set(false); j.Cts.Cancel(); } }
    public static void Pause(string id, bool paused) { lock (Running) if (Running.TryGetValue(id, out var j)) j.Pause.Set(paused); }
    public static void CancelAll() { lock (Running) foreach (var j in Running.Values) { j.Pause.Set(false); j.Cts.Cancel(); } }
    public static bool Busy { get { lock (Running) return Running.Count > 0; } }

    static void Emit(string ev, JsonObject data) { try { Event?.Invoke(ev, data); } catch { } }

    // ---------------- Çalıştırma ----------------
    static async Task RunAsync(Job job)
    {
        var sw = Stopwatch.StartNew();
        job.Temp = Path.Combine(Path.GetTempPath(), "Formaty", job.Id);
        Directory.CreateDirectory(job.Temp);
        Emit("job-start", new JsonObject { ["id"] = job.Id, ["total"] = job.Items.Count });
        try
        {
            if (!Ffmpeg.Available && NeedsFfmpeg(job)) throw new InvalidOperationException(Text.T("err.engine"));
            switch (job.Op)
            {
                case "merge": case "concat": case "imagesToPdf": case "compose":
                    await CombineAsync(job); break;
                default:
                    await PerFileAsync(job); break;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { ErrorLog.Write(ex); Emit("job-error", new JsonObject { ["id"] = job.Id, ["message"] = ex.Message }); }
        finally
        {
            lock (Running) Running.Remove(job.Id);
            try { Directory.Delete(job.Temp, true); } catch { }
            Emit("job-done", new JsonObject
            {
                ["id"] = job.Id, ["done"] = job.Done, ["failed"] = job.Failed, ["skipped"] = job.Skipped, ["cancelled"] = job.Cts.IsCancellationRequested,
                ["inBytes"] = job.InBytes, ["outBytes"] = job.OutBytes, ["seconds"] = Math.Round(sw.Elapsed.TotalSeconds, 1),
                ["outputs"] = new JsonArray(job.Outputs.Take(500).Select(x => (JsonNode)x).ToArray()),
            });
        }
    }

    static bool NeedsFfmpeg(Job job) => job.Op is "frames" or "concat"
        || job.Items.Any(i => Formats.KindOf(i.Path) is Kind.Video or Kind.Audio)
        || job.Format is "jxl" or "jp2" || Media.VideoTargets.Contains(job.Format) || Media.AudioTargets.Contains(job.Format);

    static int AutoParallel(Job job)
    {
        if (job.Parallel > 0) return Math.Clamp(job.Parallel, 1, 16);
        var kinds = job.Items.Select(i => Formats.KindOf(i.Path)).Distinct().ToList();
        int cpu = Environment.ProcessorCount;
        if (kinds.Contains(Kind.Video) && !Media.AudioTargets.Contains(job.Format)) return 1; // video kodlama kendisi çok çekirdekli
        if (kinds.Contains(Kind.Pdf)) return 1;
        return Math.Clamp(cpu / 2, 1, 6);
    }

    static async Task PerFileAsync(Job job)
    {
        using var sem = new SemaphoreSlim(AutoParallel(job));
        var tasks = new List<Task>();
        for (int i = 0; i < job.Items.Count; i++)
        {
            await job.Pause.WaitAsync(job.Cts.Token);
            await sem.WaitAsync(job.Cts.Token);
            int index = i;
            tasks.Add(Task.Run(async () =>
            {
                try { await OneAsync(job, index); }
                finally { sem.Release(); }
            }));
        }
        await Task.WhenAll(tasks);
    }

    /// <summary>Tek dosyayı işler; sonucu bildirir. Hiçbir zaman atmaz (iptal dışında).</summary>
    static async Task OneAsync(Job job, int index)
    {
        var (src, rel) = job.Items[index];
        var ct = job.Cts.Token;
        long lastEmit = 0;
        void prog(double p)
        {
            long now = Environment.TickCount64;
            if (now - lastEmit < 150 && p < 1) return;
            lastEmit = now;
            Emit("item", new JsonObject { ["id"] = job.Id, ["index"] = index, ["status"] = "working", ["progress"] = Math.Round(p, 4) });
        }
        Emit("item", new JsonObject { ["id"] = job.Id, ["index"] = index, ["status"] = "working", ["progress"] = 0 });
        var tmpFiles = new List<string>();
        string? placeholder = null, outFolder = null;
        bool ok = false;
        try
        {
            if (!File.Exists(src)) throw new FileNotFoundException(Text.T("err.unreadable"));
            long inSize = new FileInfo(src).Length;
            var kind = Formats.KindOf(src);
            var outs = new List<string>();
            string fmt = job.Format;

            // Çok çıktılı işlemler: klasöre yazar
            if (job.Op is "split" or "pdfToImages" or "frames" || (kind == Kind.Pdf && Images.Targets.Contains(fmt) && fmt != "pdf"))
            {
                var dir = OutDir(job, src, rel);
                var baseName = Path.GetFileNameWithoutExtension(src);
                var sub = Unique(Path.Combine(dir, baseName), isDir: true);
                outFolder = sub;
                var pw = job.O.S("password");
                switch (job.Op)
                {
                    case "split": outs = PdfOps.Split(src, sub, baseName, job.O, pw, prog, ct); break;
                    case "frames":
                        var info = await Ffmpeg.InfoAsync(src, ct);
                        if (!info.HasVideo) throw new InvalidOperationException(Text.T("err.noVideo"));
                        await Media.FramesAsync(src, sub, baseName, job.O, info, prog, ct, job.Pause);
                        outs = [.. Directory.GetFiles(sub)];
                        break;
                    default: outs = await PdfOps.ToImagesAsync(src, sub, baseName, fmt, job.O, pw, job.Temp, prog, ct); break;
                }
                ok = outs.Count > 0;
                Finish(job, index, src, inSize, outs, outs.Count == 0 ? null : sub);
                return;
            }

            var ext = Formats.OutExt(fmt is "" ? Formats.Ext(src) : fmt);
            var dst = TargetPath(job, src, rel, ext, index);
            if (dst == null) { Skip(job, index); return; }
            var tmp = TempBeside(dst);
            tmpFiles.Add(tmp);
            placeholder = new FileInfo(dst).Length == 0 ? dst : null;

            switch (job.Op)
            {
                case "compress" when kind == Kind.Pdf: PdfOps.Compress(src, tmp, job.O, job.O.S("password"), prog, ct); break;
                case "protect": PdfOps.Protect(src, tmp, job.O.S("password"), job.O.S("userPassword"), job.O.S("ownerPassword"), false, job.O.Sub("permissions")); break;
                case "unlock": PdfOps.Protect(src, tmp, job.O.S("password"), "", "", true, job.O); break;
                default:
                    if (kind == Kind.Image && fmt == "pdf") await PdfOps.ImagesToPdfAsync([src], tmp, job.O, job.Temp, prog, ct);
                    else if (kind == Kind.Image && Images.Targets.Contains(fmt)) await Images.ConvertAsync(src, tmp, fmt, job.O, job.Temp, ct);
                    else if (kind is Kind.Video or Kind.Image && Media.VideoTargets.Contains(fmt == "webp" ? "webp-anim" : fmt))
                    {
                        var info = await Ffmpeg.InfoAsync(src, ct);
                        if (!info.HasVideo) throw new InvalidOperationException(Text.T("err.noVideo"));
                        await Media.ConvertVideoAsync(src, tmp, fmt == "webp" ? "webp-anim" : fmt, job.O, info, prog, ct, job.Pause);
                    }
                    else if (kind is Kind.Video or Kind.Audio && Media.AudioTargets.Contains(fmt))
                    {
                        var info = await Ffmpeg.InfoAsync(src, ct);
                        await Media.ConvertAudioAsync(src, tmp, fmt, job.O, info, prog, ct, job.Pause);
                    }
                    else throw new InvalidOperationException(Text.T("err.format"));
                    break;
            }
            ct.ThrowIfCancellationRequested();
            File.Move(tmp, dst, overwrite: true);
            tmpFiles.Clear();
            File.SetAttributes(dst, File.GetAttributes(dst) & ~FileAttributes.Hidden);
            if (job.Out.B("keepDates", true)) { try { File.SetLastWriteTime(dst, File.GetLastWriteTime(src)); File.SetCreationTime(dst, File.GetCreationTime(src)); } catch { } }
            ok = true;
            Finish(job, index, src, inSize, [dst], null);
        }
        catch (OperationCanceledException)
        {
            Emit("item", new JsonObject { ["id"] = job.Id, ["index"] = index, ["status"] = "cancelled" });
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref job.Failed);
            if (ex is not (InvalidOperationException or IOException or UnauthorizedAccessException or FileNotFoundException)) ErrorLog.Write(ex);
            Emit("item", new JsonObject { ["id"] = job.Id, ["index"] = index, ["status"] = "error", ["error"] = Friendly(ex) });
        }
        finally
        {
            foreach (var t in tmpFiles) { try { if (File.Exists(t)) File.Delete(t); } catch { } }
            if (!ok)
            {
                // Başarısızlıkta ayrılan boş ad ve boş klasör kalmasın
                try { if (placeholder != null && File.Exists(placeholder) && new FileInfo(placeholder).Length == 0) File.Delete(placeholder); } catch { }
                try { if (outFolder != null && Directory.Exists(outFolder) && !Directory.EnumerateFileSystemEntries(outFolder).Any()) Directory.Delete(outFolder); } catch { }
            }
        }
    }

    static void Finish(Job job, int index, string src, long inSize, List<string> outs, string? folder)
    {
        long outSize = outs.Sum(f => { try { return new FileInfo(f).Length; } catch { return 0L; } });
        Interlocked.Add(ref job.InBytes, inSize);
        Interlocked.Add(ref job.OutBytes, outSize);
        Interlocked.Increment(ref job.Done);
        lock (job.Outputs) job.Outputs.AddRange(folder != null ? [folder] : outs);
        if (job.Out.B("recycle") && outs.Count > 0 && !outs.Any(o => o.Equals(src, StringComparison.OrdinalIgnoreCase)))
        {
            try { Native.Recycle(src); } catch { }
        }
        Emit("item", new JsonObject
        {
            ["id"] = job.Id, ["index"] = index, ["status"] = "done", ["progress"] = 1, ["inSize"] = inSize, ["outSize"] = outSize,
            ["out"] = folder ?? outs.FirstOrDefault(), ["count"] = outs.Count,
        });
    }

    static void Skip(Job job, int index)
    {
        Interlocked.Increment(ref job.Skipped);
        Emit("item", new JsonObject { ["id"] = job.Id, ["index"] = index, ["status"] = "skipped", ["error"] = Text.T("skip.exists") });
    }

    static string Friendly(Exception ex) => ex switch
    {
        IOException io when (io.HResult & 0xFFFF) is 0x70 or 0x27 => Text.T("err.disk"),
        UnauthorizedAccessException or IOException when ex.Message.Contains("being used", StringComparison.OrdinalIgnoreCase) => Text.T("err.access"),
        UnauthorizedAccessException => Text.T("err.access"),
        NetVips.VipsException => Text.T("err.unreadable"),
        _ => ex.Message,
    };

    // ---------------- Birleştirme işleri (çok giriş → tek çıktı) ----------------
    static async Task CombineAsync(Job job)
    {
        var ct = job.Cts.Token;
        var srcs = job.Items.Select(i => i.Path).Where(File.Exists).ToList();
        if (srcs.Count == 0) return;
        for (int i = 0; i < job.Items.Count; i++) Emit("item", new JsonObject { ["id"] = job.Id, ["index"] = i, ["status"] = "working", ["progress"] = 0 });
        string fmt = job.Op switch { "imagesToPdf" or "merge" or "compose" => "pdf", _ => job.Format };
        var name = job.O.S("name") is { Length: > 0 } n ? SafeName(n) : Path.GetFileNameWithoutExtension(srcs[0]) + (GrapekitApp.Lang == 0 ? "_birlesik" : "_combined");
        var dir = OutDir(job, srcs[0], job.Items[0].Rel);
        Directory.CreateDirectory(dir);
        var dst = Unique(Path.Combine(dir, name + "." + Formats.OutExt(fmt)));
        var tmp = TempBeside(dst);
        long last = 0;
        void prog(double p)
        {
            long now = Environment.TickCount64;
            if (now - last < 150 && p < 1) return;
            last = now;
            Emit("combine", new JsonObject { ["id"] = job.Id, ["progress"] = Math.Round(p, 4) });
        }
        try
        {
            switch (job.Op)
            {
                case "imagesToPdf": await PdfOps.ImagesToPdfAsync(srcs, tmp, job.O, job.Temp, prog, ct); break;
                case "merge":
                {
                    var pw = job.O.Sub("passwords");
                    var pwMap = srcs.ToDictionary(s => s, s => pw.S(s), StringComparer.OrdinalIgnoreCase);
                    var pages = srcs.SelectMany(s => PdfOps.AllPages(s, pwMap[s])).ToList();
                    PdfOps.Compose(pages, tmp, pwMap, job.O.Sub("meta"), prog, ct);
                    break;
                }
                case "compose":
                {
                    var pages = (job.O.Raw["pages"] as JsonArray ?? []).OfType<JsonObject>()
                        .Select(p => new PdfOps.PageRef(new Opt(p).S("file"), new Opt(p).I("page"), new Opt(p).I("rotate"))).ToList();
                    var pw = job.O.Sub("passwords");
                    PdfOps.Compose(pages, tmp, pages.Select(p => p.File).Distinct().ToDictionary(s => s, s => pw.S(s), StringComparer.OrdinalIgnoreCase), job.O.Sub("meta"), prog, ct);
                    break;
                }
                default: await Media.ConcatAsync(srcs, tmp, fmt, job.O, prog, ct, job.Pause); break;
            }
            ct.ThrowIfCancellationRequested();
            File.Move(tmp, dst, overwrite: true);
            File.SetAttributes(dst, File.GetAttributes(dst) & ~FileAttributes.Hidden);
            job.InBytes = srcs.Sum(s => new FileInfo(s).Length);
            job.OutBytes = new FileInfo(dst).Length;
            job.Done = srcs.Count;
            job.Outputs.Add(dst);
            for (int i = 0; i < job.Items.Count; i++) Emit("item", new JsonObject { ["id"] = job.Id, ["index"] = i, ["status"] = "done", ["progress"] = 1 });
            Emit("combine", new JsonObject { ["id"] = job.Id, ["progress"] = 1, ["out"] = dst, ["outSize"] = job.OutBytes });
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            job.Failed = srcs.Count;
            if (ex is not (InvalidOperationException or IOException)) ErrorLog.Write(ex);
            for (int i = 0; i < job.Items.Count; i++) Emit("item", new JsonObject { ["id"] = job.Id, ["index"] = i, ["status"] = "error", ["error"] = Friendly(ex) });
        }
        finally
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            try { if (job.Outputs.Count == 0 && File.Exists(dst) && new FileInfo(dst).Length == 0) File.Delete(dst); } catch { }
        }
    }

    // ---------------- Yollar ----------------
    static string OutDir(Job job, string src, string rel)
    {
        var srcDir = Path.GetDirectoryName(src)!;
        string dir = job.Out.S("mode", "same") switch
        {
            "sub" => Path.Combine(srcDir, SafeName(job.Out.S("sub", Text.T("sub.default")))),
            "custom" when job.Out.S("dir") is { Length: > 0 } d => job.Out.B("keepStructure", true) && Path.GetDirectoryName(rel) is { Length: > 0 } rd ? Path.Combine(d, rd) : d,
            _ => srcDir,
        };
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>Hedef dosya yolu; çakışma kuralına göre. null = atla.</summary>
    static string? TargetPath(Job job, string src, string rel, string ext, int index)
    {
        var dir = OutDir(job, src, rel);
        var tpl = job.Out.S("template", "{name}");
        var name = tpl.Replace("{name}", Path.GetFileNameWithoutExtension(src), StringComparison.OrdinalIgnoreCase)
            .Replace("{n}", (index + 1).ToString().PadLeft(Math.Max(2, job.Items.Count.ToString().Length), '0'), StringComparison.OrdinalIgnoreCase)
            .Replace("{date}", DateTime.Now.ToString("yyyy-MM-dd"), StringComparison.OrdinalIgnoreCase)
            .Replace("{format}", ext, StringComparison.OrdinalIgnoreCase);
        var dst = Path.Combine(dir, SafeName(name) + "." + ext);
        bool same = dst.Equals(src, StringComparison.OrdinalIgnoreCase);
        if (!File.Exists(dst)) return Unique(dst); // boşsa da yer tut: paralel işler aynı adı almasın
        return job.Out.S("conflict", "rename") switch
        {
            "skip" when !same => null,
            "overwrite" when !same => dst,
            _ => Unique(dst),
        };
    }

    /// <summary>Hedefin yanında gizli geçici dosya (uzantı korunur: FFmpeg biçimi uzantıdan anlar).</summary>
    static string TempBeside(string dst)
    {
        var tmp = Path.Combine(Path.GetDirectoryName(dst)!, "." + Path.GetFileNameWithoutExtension(dst) + ".formaty-" + Guid.NewGuid().ToString("N")[..6] + Path.GetExtension(dst));
        return tmp;
    }

    static readonly object UniqueLock = new();
    /// <summary>"ad (2).uzantı" biçiminde boş bir yol (dosya ya da klasör). Eşzamanlı işler aynı adı alamasın diye yer tutucu oluşturur.</summary>
    public static string Unique(string path, bool isDir = false)
    {
        lock (UniqueLock)
        {
            var dir = Path.GetDirectoryName(path)!; var name = isDir ? Path.GetFileName(path) : Path.GetFileNameWithoutExtension(path); var ext = isDir ? "" : Path.GetExtension(path);
            string p = path;
            for (int i = 2; File.Exists(p) || Directory.Exists(p); i++) p = Path.Combine(dir, $"{name} ({i}){ext}");
            if (isDir) Directory.CreateDirectory(p); else File.WriteAllBytes(p, []);
            return p;
        }
    }

    public static string SafeName(string n)
    {
        n = string.Concat(n.Where(c => !Path.GetInvalidFileNameChars().Contains(c))).Trim().TrimEnd('.');
        return n.Length == 0 ? "formaty" : n;
    }
}
