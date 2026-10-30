using System.Drawing.Imaging;
using NetVips;
using Img = NetVips.Image;

namespace Formaty.Engines;

/// <summary>
/// Fotoğraf dönüştürme (libvips). libvips'in açamadığı biçimler Windows (BMP/ICO) ya da FFmpeg (JPEG XL, JPEG 2000, TGA…)
/// ile PNG'ye çözülüp devam edilir; JPEG XL / JPEG 2000 yazma da FFmpeg'le yapılır.
/// </summary>
public static class Images
{
    public static readonly HashSet<string> Targets = ["jpg", "png", "webp", "avif", "gif", "tiff", "bmp", "ico", "jxl", "jp2", "pdf"];
    static readonly HashSet<string> AnimTargets = ["gif", "webp"];

    static int _init;
    public static void Init()
    {
        if (Interlocked.Exchange(ref _init, 1) == 1) return;
        // Önbellek küçük: toplu işte bellek birikmesin
        Cache.Max = 50;
        Cache.MaxMem = 64 * 1024 * 1024;
        Cache.MaxFiles = 10;
    }

    // ---------------- Yükleme ----------------
    /// <summary>Görseli açar. animated: GIF/WebP'nin bütün karelerini yükler (hareket korunacaksa).</summary>
    public static async Task<Img> LoadAsync(string path, bool animated, string temp, CancellationToken ct)
    {
        Init();
        var ext = Formats.Ext(path);
        if (Formats.VipsImage.Contains(ext))
        {
            var opts = new VOption();
            if (animated && ext is "gif" or "webp") opts.Add("n", -1);
            if (ext == "svg") opts.Add("dpi", 144);
            return Img.NewFromFile(path, access: Enums.Access.Random, kwargs: opts);
        }
        if (Formats.GdiImage.Contains(ext)) return FromGdi(path);
        // FFmpeg ile PNG'ye çöz
        var png = Path.Combine(temp, Guid.NewGuid().ToString("N") + ".png");
        await Ffmpeg.RunAsync(["-i", path, "-frames:v", "1", "-update", "1", png], 0, null, ct);
        var bytes = await File.ReadAllBytesAsync(png, ct);
        try { File.Delete(png); } catch { }
        return Img.NewFromBuffer(bytes);
    }

    static Img FromGdi(string path)
    {
        System.Drawing.Bitmap bmp;
        if (Formats.Ext(path) is "ico" or "cur")
        {
            using var icon = new System.Drawing.Icon(path, 512, 512); // en büyük boyutu seçer
            bmp = icon.ToBitmap();
        }
        else bmp = new System.Drawing.Bitmap(path);
        using (bmp)
        {
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            return Img.NewFromBuffer(ms.ToArray());
        }
    }

    public static (int W, int H, int Pages) Header(string path)
    {
        Init();
        var ext = Formats.Ext(path);
        if (Formats.VipsImage.Contains(ext))
        {
            using var im = Img.NewFromFile(path, access: Enums.Access.Sequential);
            int pages = im.Contains("n-pages") ? (int)im.Get("n-pages") : 1;
            bool rot = im.Contains("orientation") && (int)im.Get("orientation") is >= 5 and <= 8;
            return rot ? (im.Height, im.Width, pages) : (im.Width, im.Height, pages);
        }
        if (Formats.GdiImage.Contains(ext)) { using var im = FromGdi(path); return (im.Width, im.Height, 1); }
        return (0, 0, 1);
    }

    // ---------------- Dönüştürme ----------------
    public static async Task ConvertAsync(string src, string dst, string fmt, Opt o, string temp, CancellationToken ct)
    {
        bool animated = AnimTargets.Contains(fmt) && o.B("keepAnimation", true) && Formats.Ext(src) is "gif" or "webp";
        using var loaded = await LoadAsync(src, animated, temp, ct);
        ct.ThrowIfCancellationRequested();
        var im = Prepare(loaded, fmt, o, animated);
        try
        {
            await SaveAsync(im, dst, fmt, o, temp, ct);
        }
        finally { if (!ReferenceEquals(im, loaded)) im.Dispose(); }
    }

    /// <summary>Döndürme, boyutlandırma, renk alanı, saydamlık ve filigran.</summary>
    public static Img Prepare(Img im, string fmt, Opt o, bool animated)
    {
        int pageH = animated && im.Contains("page-height") ? (int)im.Get("page-height") : im.Height;
        int pages = Math.Max(1, im.Height / Math.Max(1, pageH));
        if (!animated && o.B("autoRotate", true)) im = im.Autorot();

        // Renk alanı: CMYK, 16 bit vb. → sRGB 8 bit
        if (im.Interpretation is Enums.Interpretation.Cmyk or Enums.Interpretation.Lab or Enums.Interpretation.Rgb16 or Enums.Interpretation.Grey16 or Enums.Interpretation.Scrgb)
            im = im.Colourspace(Enums.Interpretation.Srgb);
        if (im.Format != Enums.BandFormat.Uchar) im = im.Cast(Enums.BandFormat.Uchar);

        // Boyutlandırma
        var r = o.Sub("resize");
        string mode = r.S("mode", "none");
        if (mode != "none")
        {
            int W = im.Width, H = pageH;
            double sx = 1, sy = 1; bool crop = false; int tw = 0, th = 0;
            bool up = r.B("upscale");
            switch (mode)
            {
                case "fit":
                {
                    int w = r.I("width"), h = r.I("height");
                    double s = Math.Min(w > 0 ? (double)w / W : double.MaxValue, h > 0 ? (double)h / H : double.MaxValue);
                    if (s == double.MaxValue) s = 1;
                    sx = sy = s; break;
                }
                case "exact":
                {
                    tw = Math.Max(1, r.I("width", W)); th = Math.Max(1, r.I("height", H));
                    if (r.B("stretch")) { sx = (double)tw / W; sy = (double)th / H; }
                    else { sx = sy = Math.Max((double)tw / W, (double)th / H); crop = true; }
                    up = true; break;
                }
                case "percent": sx = sy = Math.Max(1, r.N("percent", 100)) / 100; up = true; break;
                case "long": sx = sy = (double)Math.Max(1, r.I("long", Math.Max(W, H))) / Math.Max(W, H); break;
            }
            if (!up) { sx = Math.Min(1, sx); sy = Math.Min(1, sy); }
            if (Math.Abs(sx - 1) > 1e-6 || Math.Abs(sy - 1) > 1e-6)
            {
                if (animated && pages > 1)
                {
                    // Kareler tek uzun şerit: her kareyi ayrı boyutlandırıp yeniden birleştir
                    var frames = Enumerable.Range(0, pages).Select(i => im.Crop(0, i * pageH, W, pageH).Resize(sx, vscale: sy, kernel: Enums.Kernel.Lanczos3)).ToArray();
                    int nh = frames[0].Height;
                    im = Img.Arrayjoin(frames, across: 1);
                    im = im.Mutate(m => m.Set("page-height", nh));
                    pageH = nh;
                }
                else im = im.Resize(sx, vscale: sy, kernel: Enums.Kernel.Lanczos3);
            }
            if (crop && !animated && (im.Width > tw || im.Height > th))
                im = im.Crop(Math.Max(0, (im.Width - tw) / 2), Math.Max(0, (im.Height - th) / 2), Math.Min(tw, im.Width), Math.Min(th, im.Height));
        }

        // Döndür / çevir
        switch (o.S("rotate"))
        {
            case "90": im = im.Rot90(); break;
            case "180": im = im.Rot180(); break;
            case "270": im = im.Rot270(); break;
            case "hflip": im = im.Flip(Enums.Direction.Horizontal); break;
            case "vflip": im = im.Flip(Enums.Direction.Vertical); break;
        }

        // Filigran (metin)
        var wm = o.Sub("watermark");
        if (wm.S("text") is { Length: > 0 } wtext && !animated) im = Watermark(im, wtext, wm);

        // Saydamlık desteklemeyen biçimler: arka plan rengiyle düzleştir
        if (im.HasAlpha() && fmt is "jpg" or "bmp" or "pdf" || (im.HasAlpha() && o.B("flatten")))
        {
            var bg = ParseColor(o.S("background", "#FFFFFF"));
            im = im.Flatten(background: bg);
        }
        return im;
    }

    static Img Watermark(Img im, string text, Opt wm)
    {
        double size = Math.Max(8, Math.Min(im.Width, im.Height) * wm.N("size", 4) / 100);
        var opacity = Math.Clamp(wm.N("opacity", 60), 5, 100) / 100;
        using var t = Img.Text($"<span foreground=\"white\">{System.Security.SecurityElement.Escape(text)}</span>", font: $"Sans Bold {(int)size}", rgba: true, dpi: 72);
        var mark = t * new double[] { 1, 1, 1, opacity };
        int m = (int)(size * 0.8);
        var (x, y) = wm.S("position", "br") switch
        {
            "tl" => (m, m), "tr" => (im.Width - mark.Width - m, m), "bl" => (m, im.Height - mark.Height - m),
            "c" => ((im.Width - mark.Width) / 2, (im.Height - mark.Height) / 2),
            _ => (im.Width - mark.Width - m, im.Height - mark.Height - m),
        };
        var basei = im.HasAlpha() ? im : im.Bandjoin(255);
        if (basei.Bands == 2) basei = basei.Colourspace(Enums.Interpretation.Srgb);
        var res = basei.Composite2(mark, Enums.BlendMode.Over, x: Math.Max(0, x), y: Math.Max(0, y));
        return im.HasAlpha() ? res : res.ExtractBand(0, n: 3);
    }

    static double[] ParseColor(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length != 6) return [255, 255, 255];
        return [Convert.ToInt32(hex[..2], 16), Convert.ToInt32(hex[2..4], 16), Convert.ToInt32(hex[4..6], 16)];
    }

    // ---------------- Kaydetme ----------------
    public static async Task SaveAsync(Img im, string dst, string fmt, Opt o, string temp, CancellationToken ct)
    {
        int q = Math.Clamp(o.I("quality", 82), 1, 100);
        double targetKb = o.N("targetKb", 0);
        if (targetKb > 0 && fmt is "jpg" or "webp" or "avif")
        {
            // Hedef boyut: kaliteyi ikili aramayla bul (en az 10)
            int lo = 10, hi = q, best = lo; byte[]? bestBuf = null;
            while (lo <= hi)
            {
                ct.ThrowIfCancellationRequested();
                int mid = (lo + hi) / 2;
                var buf = Encode(im, fmt, mid, o);
                if (buf.Length <= targetKb * 1024) { best = mid; bestBuf = buf; lo = mid + 1; } else hi = mid - 1;
            }
            await File.WriteAllBytesAsync(dst, bestBuf ?? Encode(im, fmt, 10, o), ct);
            return;
        }
        switch (fmt)
        {
            case "jpg": case "png": case "webp": case "avif": case "gif": case "tiff":
                await File.WriteAllBytesAsync(dst, Encode(im, fmt, q, o), ct);
                break;
            case "bmp":
            {
                using var ms = new MemoryStream(im.PngsaveBuffer(compression: 1));
                using var bmp = new System.Drawing.Bitmap(ms);
                bmp.Save(dst, ImageFormat.Bmp);
                break;
            }
            case "ico": await File.WriteAllBytesAsync(dst, Ico(im, o), ct); break;
            case "jxl": case "jp2":
            {
                var png = Path.Combine(temp, Guid.NewGuid().ToString("N") + ".png");
                im.Pngsave(png, compression: 1);
                try
                {
                    var args = new List<string> { "-i", png };
                    if (fmt == "jxl")
                    {
                        // JPEG XL "distance": 0 kayıpsız, 1 görsel olarak kayıpsız; kaliteden çevrilir
                        double d = o.B("lossless") ? 0 : Math.Clamp((100 - q) / 10.0, 0.1, 15);
                        args.AddRange(["-c:v", "libjxl", "-distance", Ffmpeg.F(d), "-effort", "7"]);
                    }
                    else args.AddRange(["-c:v", "libopenjpeg", "-compression_level", Math.Clamp((100 - q) / 3, 0, 40).ToString()]);
                    args.AddRange(["-frames:v", "1", "-update", "1", dst]);
                    await Ffmpeg.RunAsync(args, 0, null, ct);
                }
                finally { try { File.Delete(png); } catch { } }
                break;
            }
            default: throw new InvalidOperationException(Text.T("err.format"));
        }
    }

    static Enums.ForeignKeep Keep(Opt o) => o.B("keepMeta", false) ? Enums.ForeignKeep.All : Enums.ForeignKeep.Icc;

    public static byte[] Encode(Img im, string fmt, int q, Opt o) => fmt switch
    {
        "jpg" => im.JpegsaveBuffer(q: q, optimizeCoding: true, interlace: o.B("progressive", true), keep: Keep(o),
            subsampleMode: q >= 90 ? Enums.ForeignSubsample.Off : Enums.ForeignSubsample.Auto),
        "png" => o.B("palette") ? im.PngsaveBuffer(compression: o.I("compression", 6), palette: true, q: q, keep: Keep(o))
                                : im.PngsaveBuffer(compression: o.I("compression", 6), keep: Keep(o)),
        "webp" => im.WebpsaveBuffer(q: q, lossless: o.B("lossless"), effort: o.I("effort", 4), keep: Keep(o)),
        "avif" => im.HeifsaveBuffer(q: q, lossless: o.B("lossless"), compression: Enums.ForeignHeifCompression.Av1, effort: o.I("effort", 4), keep: Keep(o)),
        "gif" => im.GifsaveBuffer(effort: 7, bitdepth: 8, keep: Keep(o)),
        "tiff" => im.TiffsaveBuffer(compression: o.S("tiffCompression", "lzw") switch
        {
            "none" => Enums.ForeignTiffCompression.None, "deflate" => Enums.ForeignTiffCompression.Deflate,
            "jpeg" => Enums.ForeignTiffCompression.Jpeg, _ => Enums.ForeignTiffCompression.Lzw,
        }, q: q, keep: Keep(o)),
        _ => throw new InvalidOperationException(Text.T("err.format")),
    };

    /// <summary>Çok boyutlu ICO (PNG kareleri, Windows Vista ve sonrası).</summary>
    static byte[] Ico(Img im, Opt o)
    {
        int[] sizes = [16, 24, 32, 48, 64, 128, 256];
        // Kare tuval: kısa kenarı saydam boşlukla tamamla
        var src = im.HasAlpha() ? im : im.Bandjoin(255);
        if (src.Bands == 2) src = src.Colourspace(Enums.Interpretation.Srgb);
        int side = Math.Max(src.Width, src.Height);
        src = src.Embed((side - src.Width) / 2, (side - src.Height) / 2, side, side, extend: Enums.Extend.Background, background: [0, 0, 0, 0]);
        var pngs = sizes.Select(s => src.Resize((double)s / side, kernel: Enums.Kernel.Lanczos3).PngsaveBuffer(compression: 9)).ToArray();
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write((ushort)0); w.Write((ushort)1); w.Write((ushort)sizes.Length);
        int offset = 6 + 16 * sizes.Length;
        for (int i = 0; i < sizes.Length; i++)
        {
            w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i])); w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
            w.Write((byte)0); w.Write((byte)0); w.Write((ushort)1); w.Write((ushort)32);
            w.Write(pngs[i].Length); w.Write(offset); offset += pngs[i].Length;
        }
        foreach (var p in pngs) w.Write(p);
        return ms.ToArray();
    }

    // ---------------- Küçük resim ----------------
    public static async Task ThumbAsync(string src, string dst, int size, string temp, CancellationToken ct)
    {
        Init();
        var ext = Formats.Ext(src);
        Img t;
        if (Formats.VipsImage.Contains(ext)) t = Img.Thumbnail(src, size, height: size, size: Enums.Size.Down);
        else { using var full = await LoadAsync(src, false, temp, ct); t = full.ThumbnailImage(size, height: size, size: Enums.Size.Down); }
        using (t)
        {
            var im = t.HasAlpha() ? t.Flatten(background: [255, 255, 255]) : t;
            if (im.Bands == 1) im = im.Colourspace(Enums.Interpretation.Srgb);
            im.Jpegsave(dst, q: 80);
        }
    }
}
