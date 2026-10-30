namespace Formaty.Engines;

/// <summary>
/// Video ve ses dönüştürme: hedef biçim + seçeneklerden FFmpeg komutu kurar.
/// Kalite düzeyleri: small / balanced / high / max. Hedef boyut (MB) verilirse bit hızı ondan hesaplanır.
/// </summary>
public static class Media
{
    public static readonly HashSet<string> VideoTargets = ["mp4", "webm", "mkv", "mov", "avi", "gif", "webp-anim"];
    public static readonly HashSet<string> AudioTargets = ["mp3", "aac", "opus", "ogg", "flac", "wav", "aiff", "alac"];

    static int Level(string q) => q switch { "small" => 0, "high" => 2, "max" => 3, _ => 1 };

    // ---------------- Video ----------------
    public static async Task ConvertVideoAsync(string src, string dst, string fmt, Opt o, Ffmpeg.MediaInfo info, Action<double> progress, CancellationToken ct, PauseToken pause)
    {
        var args = new List<string>();
        var (ss, to) = Trim(o, info);
        if (ss > 0) args.AddRange(["-ss", Ffmpeg.F(ss)]);
        if (to > 0) args.AddRange(["-to", Ffmpeg.F(to)]);
        args.AddRange(["-i", src]);
        double dur = Math.Max(0.01, (to > 0 ? to : info.Duration) - ss);
        var speed = Math.Clamp(o.N("speed", 1), 0.25, 4);
        if (speed != 1) dur /= speed;

        if (fmt is "gif" or "webp-anim") { AnimArgs(args, fmt, o, info, speed); args.Add(dst); await Ffmpeg.RunAsync(args, dur, progress, ct, pause); return; }

        string codec = o.S("vcodec", "auto");
        if (codec == "auto") codec = fmt switch { "webm" => "vp9", "avi" => "mpeg4", _ => "h264" };
        if (fmt == "webm" && codec is not ("vp9" or "av1")) codec = "vp9";
        if (fmt == "avi") codec = "mpeg4";

        args.AddRange(["-map", "0:v:0"]);
        bool audio = info.HasAudio && o.S("audio", "keep") != "remove";
        if (audio) args.AddRange(["-map", "0:a:0"]);
        args.AddRange(["-map_metadata", "0", "-sn", "-dn"]);

        // Görüntü filtreleri
        var vf = new List<string>();
        int h = o.I("height", 0), w = 0;
        if (h > 0 && (info.Height == 0 || h < Math.Min(info.Height, info.Width) || o.B("upscale")))
        {
            bool portrait = info.Width > 0 && info.Height > info.Width ^ (Math.Abs(info.Rotation) % 180 == 90);
            vf.Add(portrait ? $"scale={h}:-2:flags=lanczos" : $"scale=-2:{h}:flags=lanczos");
            w = portrait ? h : 0;
        }
        int fps = o.I("fps", 0);
        if (fps > 0 && (info.Fps == 0 || fps < info.Fps)) vf.Add($"fps={fps}");
        switch (o.S("rotate"))
        {
            case "90": vf.Add("transpose=1"); break;
            case "180": vf.Add("transpose=1,transpose=1"); break;
            case "270": vf.Add("transpose=2"); break;
            case "hflip": vf.Add("hflip"); break;
            case "vflip": vf.Add("vflip"); break;
        }
        if (speed != 1) vf.Add($"setpts=PTS/{Ffmpeg.F(speed)}");

        if (codec == "copy")
        {
            args.AddRange(["-c:v", "copy"]);
        }
        else
        {
            var enc = await Ffmpeg.PickEncoderAsync(codec, o.B("hw", true));
            bool hw = enc.Contains("_nvenc") || enc.Contains("_qsv") || enc.Contains("_amf") || enc.Contains("_mf");
            vf.Add($"format={(enc.EndsWith("_qsv") || enc.EndsWith("_mf") ? "nv12" : "yuv420p")}");
            args.AddRange(["-c:v", enc]);
            int outH = h > 0 ? h : (info.Height > 0 ? info.Height : 1080);
            int outW = w > 0 ? w : (info.Width > 0 && info.Height > 0 ? info.Width * outH / info.Height : outH * 16 / 9);
            double outFps = fps > 0 ? fps : (info.Fps > 0 ? info.Fps : 30);
            double targetMb = o.N("targetMb", 0);
            int aKbps = audio ? AudioKbpsForVideo(fmt, o) : 0;
            if (targetMb > 0)
            {
                // Hedef boyut: kapsayıcı payı için %3 ayır
                var total = targetMb * 8 * 1024 * 1024 * 0.97 / dur / 1000; // kbps
                var vk = Math.Max(80, (int)(total - aKbps));
                args.AddRange(["-b:v", vk + "k", "-maxrate", (int)(vk * 1.25) + "k", "-bufsize", vk * 2 + "k"]);
                if (enc.Contains("_nvenc")) args.AddRange(["-rc", "vbr"]);
                if (enc.Contains("_amf")) args.AddRange(["-rc", "vbr_peak"]);
                if (enc.EndsWith("_mf")) args.AddRange(["-rate_control", "u_vbr"]);
                if (enc is "libvpx-vp9" or "libvpx") args.AddRange(["-deadline", "good", "-cpu-used", "2", "-row-mt", "1"]);
                if (enc == "libsvtav1") args.AddRange(["-preset", "8"]);
            }
            else QualityArgs(args, enc, Level(o.S("quality", "balanced")), outW, outH, outFps);
            if (!hw && enc == "libopenh264") args.AddRange(["-allow_skip_frames", "0"]);
            if (codec is "h264" or "hevc") args.AddRange(["-g", ((int)Math.Round(outFps * 2)).ToString()]);
            if (codec == "hevc" && fmt is "mp4" or "mov") args.AddRange(["-tag:v", "hvc1"]);
        }
        if (vf.Count > 0 && codec != "copy") args.AddRange(["-vf", string.Join(",", vf)]);

        if (audio)
        {
            var af = AudioFilters(o.Sub("audioOpts"));
            if (speed != 1) af.Add(Atempo(speed));
            if (af.Count > 0) args.AddRange(["-af", string.Join(",", af)]);
            switch (fmt)
            {
                case "webm": args.AddRange(["-c:a", "libopus", "-b:a", AudioKbpsForVideo(fmt, o) + "k"]); break;
                case "avi": args.AddRange(["-c:a", "libmp3lame", "-b:a", AudioKbpsForVideo(fmt, o) + "k"]); break;
                default: args.AddRange(["-c:a", "aac", "-b:a", AudioKbpsForVideo(fmt, o) + "k"]); break;
            }
        }
        else args.Add("-an");
        if (fmt is "mp4" or "mov") args.AddRange(["-movflags", "+faststart"]);
        args.Add(dst);
        double target = o.N("targetMb", 0) * 1024 * 1024;
        if (target <= 0) { await Ffmpeg.RunAsync(args, dur, progress, ct, pause); return; }
        // Hedef boyut: kodlayıcılar bit hızını tam tutturamaz; aşılırsa oranla düşürüp bir kez daha dene
        await Ffmpeg.RunAsync(args, dur, p => progress(p * 0.5), ct, pause);
        long got = new FileInfo(dst).Length;
        if (got > target * 1.02)
        {
            int bi = args.IndexOf("-b:v");
            if (bi >= 0 && int.TryParse(args[bi + 1].TrimEnd('k'), out var vk))
            {
                double ratio = target / got * 0.97;
                int nk = Math.Max(60, (int)(vk * ratio - (1 - ratio) * AudioKbpsForVideo(fmt, o) * 0.2));
                args[bi + 1] = nk + "k";
                int mi = args.IndexOf("-maxrate"); if (mi >= 0) args[mi + 1] = (int)(nk * 1.25) + "k";
                int bs = args.IndexOf("-bufsize"); if (bs >= 0) args[bs + 1] = nk * 2 + "k";
                await Ffmpeg.RunAsync(args, dur, p => progress(0.5 + p * 0.5), ct, pause);
            }
        }
        progress(1);
    }

    static int AudioKbpsForVideo(string fmt, Opt o) => o.I("audioKbps", 0) is > 0 and var k ? k : Level(o.S("quality", "balanced")) switch { 0 => 96, 1 => 128, 2 => 192, _ => 256 };

    static void QualityArgs(List<string> a, string enc, int lv, int w, int h, double fps)
    {
        switch (enc)
        {
            case var e when e.EndsWith("_nvenc"):
                a.AddRange(["-preset", "p5", "-rc", "vbr", "-cq", new[] { "30", "25", "21", "17" }[lv], "-b:v", "0"]); break;
            case var e when e.EndsWith("_qsv"):
                a.AddRange(["-preset", "medium", "-global_quality", new[] { "30", "25", "21", "18" }[lv]]); break;
            case var e when e.EndsWith("_amf"):
                a.AddRange(["-quality", "quality", "-rc", "cqp", "-qp_i", new[] { "30", "25", "21", "17" }[lv], "-qp_p", new[] { "32", "27", "23", "19" }[lv]]); break;
            case var e when e.EndsWith("_mf"):
                a.AddRange(["-rate_control", "quality", "-quality", new[] { "35", "55", "75", "90" }[lv], "-hw_encoding", "1"]); break;
            case "libvpx-vp9":
                a.AddRange(["-crf", new[] { "38", "33", "28", "23" }[lv], "-b:v", "0", "-deadline", "good", "-cpu-used", "3", "-row-mt", "1"]); break;
            case "libsvtav1":
                a.AddRange(["-crf", new[] { "40", "34", "28", "22" }[lv], "-preset", "8"]); break;
            case "libkvazaar":
                a.AddRange(["-kvazaar-params", "qp=" + new[] { "34", "29", "25", "21" }[lv]]); break;
            case "mpeg4":
                a.AddRange(["-q:v", new[] { "8", "5", "3", "2" }[lv]]); break;
            default: // libopenh264: yalnız bit hızıyla; piksel başına bit
                var bpp = new[] { 0.05, 0.08, 0.12, 0.2 }[lv];
                a.AddRange(["-b:v", Math.Max(300, (int)(w * h * fps * bpp / 1000)) + "k"]); break;
        }
    }

    static void AnimArgs(List<string> args, string fmt, Opt o, Ffmpeg.MediaInfo info, double speed)
    {
        int fps = o.I("fps", 0) is > 0 and var f ? f : (fmt == "gif" ? 12 : 15);
        int w = o.I("width", 0) is > 0 and var ww ? ww : Math.Min(480, info.Width > 0 ? info.Width : 480);
        var pre = $"{(speed != 1 ? $"setpts=PTS/{Ffmpeg.F(speed)}," : "")}fps={fps},scale={w}:-2:flags=lanczos";
        if (fmt == "gif")
        {
            var dither = Level(o.S("quality", "balanced")) >= 2 ? "sierra2_4a" : "bayer:bayer_scale=4";
            args.AddRange(["-filter_complex", $"[0:v:0]{pre},split[a][b];[a]palettegen=stats_mode=diff[p];[b][p]paletteuse=dither={dither}", "-loop", o.B("loop", true) ? "0" : "-1", "-an"]);
        }
        else
        {
            args.AddRange(["-vf", pre, "-c:v", "libwebp", "-q:v", new[] { "50", "70", "82", "95" }[Level(o.S("quality", "balanced"))], "-loop", o.B("loop", true) ? "0" : "1", "-an"]);
        }
    }

    static (double ss, double to) Trim(Opt o, Ffmpeg.MediaInfo info)
    {
        double ss = Math.Max(0, o.N("start", 0)), to = o.N("end", 0);
        if (info.Duration > 0 && to >= info.Duration - 0.01) to = 0;
        if (to > 0 && to <= ss) to = 0;
        return (ss, to);
    }

    // ---------------- Ses ----------------
    public static async Task ConvertAudioAsync(string src, string dst, string fmt, Opt o, Ffmpeg.MediaInfo info, Action<double> progress, CancellationToken ct, PauseToken pause)
    {
        if (!info.HasAudio) throw new InvalidOperationException(Text.T("err.noAudio"));
        var args = new List<string>();
        var (ss, to) = Trim(o, info);
        if (ss > 0) args.AddRange(["-ss", Ffmpeg.F(ss)]);
        if (to > 0) args.AddRange(["-to", Ffmpeg.F(to)]);
        args.AddRange(["-i", src, "-map", "0:a:0"]);
        // Albüm kapağı: kapak destekleyen hedeflerde (MP3, M4A, FLAC) kapak akışı olduğu gibi kopyalanır
        bool cover = o.B("keepCover", true) && info.CoverCodec is "mjpeg" or "png" && fmt is "mp3" or "aac" or "alac" or "flac";
        if (cover) args.AddRange(["-map", "0:v:0", "-c:v", "copy", "-disposition:v:0", "attached_pic"]);
        else args.Add("-vn");
        args.AddRange(["-sn", "-dn", "-map_metadata", "0"]);
        double dur = Math.Max(0.01, (to > 0 ? to : info.Duration) - ss);
        var speed = Math.Clamp(o.N("speed", 1), 0.25, 4);
        if (speed != 1) dur /= speed;
        var af = AudioFilters(o);
        if (speed != 1) af.Add(Atempo(speed));
        if (af.Count > 0) args.AddRange(["-af", string.Join(",", af)]);
        if (o.I("sampleRate", 0) is > 0 and var sr) args.AddRange(["-ar", sr.ToString()]);
        var ch = o.S("channels", "keep");
        if (ch == "mono") args.AddRange(["-ac", "1"]); else if (ch == "stereo") args.AddRange(["-ac", "2"]);
        int lv = Level(o.S("quality", "balanced"));
        string mode = o.S("mode", "cbr"); // cbr | vbr
        int kbps = o.I("kbps", 0);
        switch (fmt)
        {
            case "mp3":
                args.AddRange(["-c:a", "libmp3lame"]);
                if (mode == "vbr") args.AddRange(["-q:a", o.I("vbr", new[] { 7, 4, 2, 0 }[lv]).ToString()]);
                else args.AddRange(["-b:a", (kbps > 0 ? kbps : new[] { 96, 160, 256, 320 }[lv]) + "k"]);
                args.AddRange(["-id3v2_version", "3"]);
                break;
            case "aac": args.AddRange(["-c:a", "aac", "-b:a", (kbps > 0 ? kbps : new[] { 96, 160, 224, 320 }[lv]) + "k", "-movflags", "+faststart"]); break;
            case "opus": args.AddRange(["-c:a", "libopus", "-b:a", (kbps > 0 ? kbps : new[] { 64, 112, 160, 256 }[lv]) + "k"]); break;
            case "ogg":
                args.AddRange(["-c:a", "libvorbis"]);
                if (kbps > 0) args.AddRange(["-b:a", kbps + "k"]); else args.AddRange(["-q:a", new[] { "3", "5", "7", "9" }[lv]]);
                break;
            case "flac":
                args.AddRange(["-c:a", "flac", "-compression_level", o.I("compression", 5).ToString()]);
                if (o.I("bits", 0) == 16) args.AddRange(["-sample_fmt", "s16"]); else if (o.I("bits", 0) == 24) args.AddRange(["-sample_fmt", "s32", "-bits_per_raw_sample", "24"]);
                break;
            case "wav":
                args.AddRange(["-c:a", o.I("bits", 16) switch { 24 => "pcm_s24le", 32 => "pcm_f32le", 8 => "pcm_u8", _ => "pcm_s16le" }]); break;
            case "aiff":
                args.AddRange(["-c:a", o.I("bits", 16) switch { 24 => "pcm_s24be", 32 => "pcm_f32be", _ => "pcm_s16be" }]); break;
            case "alac": args.AddRange(["-c:a", "alac"]); break;
            default: throw new InvalidOperationException(Text.T("err.format"));
        }
        args.Add(dst);
        await Ffmpeg.RunAsync(args, dur, progress, ct, pause);
    }

    static List<string> AudioFilters(Opt o)
    {
        var af = new List<string>();
        if (o.B("trimSilence")) af.Add("silenceremove=start_periods=1:start_threshold=-50dB:start_silence=0.2,areverse,silenceremove=start_periods=1:start_threshold=-50dB:start_silence=0.2,areverse");
        var vol = o.N("volumeDb", 0);
        if (vol != 0) af.Add($"volume={Ffmpeg.F(vol)}dB");
        var lufs = o.N("normalize", 0); // ör. -16, -14, -23; 0 = kapalı
        if (lufs < 0) af.Add($"loudnorm=I={Ffmpeg.F(lufs)}:TP=-1.5:LRA=11");
        if (o.N("fadeIn", 0) is > 0 and var fi) af.Add($"afade=t=in:d={Ffmpeg.F(fi)}");
        return af;
    }

    static string Atempo(double s)
    {
        // atempo 0.5–2 aralığında: dışındaysa zincirle
        var parts = new List<string>();
        while (s > 2) { parts.Add("atempo=2"); s /= 2; }
        while (s < 0.5) { parts.Add("atempo=0.5"); s /= 0.5; }
        parts.Add($"atempo={Ffmpeg.F(s)}");
        return string.Join(",", parts);
    }

    // ---------------- Videodan kareler ----------------
    public static async Task FramesAsync(string src, string outDir, string baseName, Opt o, Ffmpeg.MediaInfo info, Action<double> progress, CancellationToken ct, PauseToken pause)
    {
        Directory.CreateDirectory(outDir);
        var fmt = o.S("imageFormat", "jpg");
        var every = Math.Max(0.04, o.N("every", 1));
        var args = new List<string> { "-i", src, "-map", "0:v:0", "-vf", $"fps=1/{Ffmpeg.F(every)}" };
        if (fmt == "jpg") args.AddRange(["-q:v", "2"]);
        if (fmt == "webp") args.AddRange(["-c:v", "libwebp", "-q:v", "85"]);
        args.Add(Path.Combine(outDir, baseName + "_%04d." + fmt));
        await Ffmpeg.RunAsync(args, info.Duration, progress, ct, pause);
    }

    // ---------------- Birleştirme (aynı türde video ya da ses) ----------------
    public static async Task ConcatAsync(IReadOnlyList<string> inputs, string dst, string fmt, Opt o, Action<double> progress, CancellationToken ct, PauseToken pause)
    {
        var infos = new List<Ffmpeg.MediaInfo>();
        foreach (var i in inputs) infos.Add(await Ffmpeg.InfoAsync(i, ct));
        double dur = infos.Sum(x => x.Duration);
        bool video = VideoTargets.Contains(fmt) && infos.All(x => x.HasVideo);
        var args = new List<string>();
        foreach (var i in inputs) args.AddRange(["-i", i]);
        if (video)
        {
            // Farklı boyutlu videolar ilkinin boyutuna sığdırılır, boşluk siyah doldurulur
            int w = infos[0].Width > 0 ? infos[0].Width : 1280, h = infos[0].Height > 0 ? infos[0].Height : 720;
            w -= w % 2; h -= h % 2;
            bool allAudio = infos.All(x => x.HasAudio);
            var fc = new System.Text.StringBuilder();
            for (int k = 0; k < inputs.Count; k++)
            {
                fc.Append($"[{k}:v:0]scale={w}:{h}:force_original_aspect_ratio=decrease,pad={w}:{h}:(ow-iw)/2:(oh-ih)/2,setsar=1,fps=30,format=yuv420p[v{k}];");
                if (allAudio) fc.Append($"[{k}:a:0]aresample=48000,aformat=channel_layouts=stereo[a{k}];");
            }
            for (int k = 0; k < inputs.Count; k++) fc.Append($"[v{k}]{(allAudio ? $"[a{k}]" : "")}");
            fc.Append($"concat=n={inputs.Count}:v=1:a={(allAudio ? 1 : 0)}[v]{(allAudio ? "[a]" : "")}");
            args.AddRange(["-filter_complex", fc.ToString(), "-map", "[v]"]);
            if (allAudio) args.AddRange(["-map", "[a]"]);
            var codec = fmt == "webm" ? "vp9" : "h264";
            var enc = await Ffmpeg.PickEncoderAsync(codec, o.B("hw", true));
            args.AddRange(["-c:v", enc]);
            QualityArgs(args, enc, Level(o.S("quality", "balanced")), w, h, 30);
            if (allAudio) args.AddRange(fmt == "webm" ? ["-c:a", "libopus", "-b:a", "128k"] : ["-c:a", "aac", "-b:a", "192k"]);
            if (fmt is "mp4" or "mov") args.AddRange(["-movflags", "+faststart"]);
        }
        else
        {
            var fc = string.Concat(Enumerable.Range(0, inputs.Count).Select(k => $"[{k}:a:0]aresample=48000,aformat=channel_layouts=stereo[a{k}];"))
                + string.Concat(Enumerable.Range(0, inputs.Count).Select(k => $"[a{k}]")) + $"concat=n={inputs.Count}:v=0:a=1[a]";
            args.AddRange(["-filter_complex", fc, "-map", "[a]", "-vn"]);
            args.AddRange(fmt switch
            {
                "mp3" => ["-c:a", "libmp3lame", "-b:a", "256k"],
                "flac" => ["-c:a", "flac"],
                "wav" => ["-c:a", "pcm_s16le"],
                "opus" => ["-c:a", "libopus", "-b:a", "160k"],
                "ogg" => ["-c:a", "libvorbis", "-q:a", "6"],
                _ => ["-c:a", "aac", "-b:a", "224k"],
            });
        }
        args.Add(dst);
        await Ffmpeg.RunAsync(args, dur, progress, ct, pause);
    }

    // ---------------- Küçük resim ----------------
    public static async Task ThumbAsync(string src, string dst, int size, double at, CancellationToken ct)
    {
        var args = new List<string> { "-ss", Ffmpeg.F(at), "-i", src, "-map", "0:v:0", "-frames:v", "1", "-vf", $"scale='min({size},iw)':-2", "-q:v", "4", dst };
        await Ffmpeg.RunAsync(args, 0, null, ct);
    }
}
