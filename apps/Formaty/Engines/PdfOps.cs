using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;
using PdfSharp.Pdf.Security;
using Img = NetVips.Image;

namespace Formaty.Engines;

/// <summary>PDF işlemleri: birleştirme, sayfa düzeni, bölme, şifre, görsellerden PDF, PDF'ten görsel, küçültme.</summary>
public static class PdfOps
{
    public sealed record PageRef(string File, int Page, int Rotate);

    static PdfDocument OpenImport(string path, string? password)
    {
        try { return password is { Length: > 0 } ? PdfReader.Open(path, password, PdfDocumentOpenMode.Import) : PdfReader.Open(path, PdfDocumentOpenMode.Import); }
        catch (PdfReaderException ex) when (ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase)) { throw new InvalidOperationException(Text.T("err.pdfPassword")); }
    }

    public static int PageCount(string path, string? password = null)
    {
        try { using var d = PdfReader.Open(path, password ?? "", PdfDocumentOpenMode.Import); return d.PageCount; }
        catch { using var d = Pdfium.Open(path, password); return d.Pages; }
    }

    static PdfDocument NewDoc(PdfDocument? like = null)
    {
        var d = new PdfDocument();
        d.Options.CompressContentStreams = true;
        d.Options.FlateEncodeMode = PdfFlateEncodeMode.BestCompression;
        if (like != null)
        {
            d.Info.Title = like.Info.Title; d.Info.Author = like.Info.Author; d.Info.Subject = like.Info.Subject; d.Info.Keywords = like.Info.Keywords;
        }
        d.Info.Creator = "Formaty";
        return d;
    }

    /// <summary>Sayfa listesinden yeni PDF: birleştirme, sıralama, silme, ayıklama, döndürme hepsi bununla.</summary>
    public static void Compose(IReadOnlyList<PageRef> pages, string dst, IReadOnlyDictionary<string, string>? passwords, Opt meta, Action<double> progress, CancellationToken ct)
    {
        var open = new Dictionary<string, PdfDocument>(StringComparer.OrdinalIgnoreCase);
        try
        {
            PdfDocument? first = null;
            foreach (var f in pages.Select(p => p.File).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var d = OpenImport(f, passwords?.GetValueOrDefault(f));
                open[f] = d; first ??= d;
            }
            using var outDoc = NewDoc(first);
            for (int i = 0; i < pages.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var r = pages[i];
                var src = open[r.File];
                if (r.Page < 0 || r.Page >= src.PageCount) continue;
                var page = outDoc.AddPage(src.Pages[r.Page]);
                if (r.Rotate != 0) page.Rotate = ((page.Rotate + r.Rotate) % 360 + 360) % 360;
                progress((i + 1.0) / pages.Count);
            }
            if (meta.S("title") is { Length: > 0 } t) outDoc.Info.Title = t;
            if (meta.S("author") is { Length: > 0 } a) outDoc.Info.Author = a;
            outDoc.Save(dst);
        }
        finally { foreach (var d in open.Values) d.Dispose(); }
    }

    public static IEnumerable<PageRef> AllPages(string file, string? password)
    {
        int n = PageCount(file, password);
        for (int i = 0; i < n; i++) yield return new PageRef(file, i, 0);
    }

    /// <summary>"1-3, 5, 8-" → 0 tabanlı sayfa grupları.</summary>
    public static List<List<int>> ParseRanges(string spec, int count)
    {
        var groups = new List<List<int>>();
        foreach (var part in spec.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var g = new List<int>();
            var bits = part.Split('-', StringSplitOptions.TrimEntries);
            int a = bits[0].Length == 0 ? 1 : int.Parse(bits[0]);
            int b = bits.Length == 1 ? a : bits[1].Length == 0 ? count : int.Parse(bits[1]);
            if (a > b) (a, b) = (b, a);
            for (int k = Math.Max(1, a); k <= Math.Min(count, b); k++) g.Add(k - 1);
            if (g.Count > 0) groups.Add(g);
        }
        if (groups.Count == 0) throw new InvalidOperationException(Text.T("err.pdfRange"));
        return groups;
    }

    /// <summary>Bölme: mode = each | every | ranges. Çıktı yolları döner.</summary>
    public static List<string> Split(string src, string outDir, string baseName, Opt o, string? password, Action<double> progress, CancellationToken ct)
    {
        using var doc = OpenImport(src, password);
        int n = doc.PageCount;
        List<List<int>> groups = o.S("mode", "each") switch
        {
            "every" => Enumerable.Range(0, (n + Math.Max(1, o.I("every", 1)) - 1) / Math.Max(1, o.I("every", 1)))
                .Select(g => Enumerable.Range(g * Math.Max(1, o.I("every", 1)), Math.Min(Math.Max(1, o.I("every", 1)), n - g * Math.Max(1, o.I("every", 1)))).ToList()).ToList(),
            "ranges" => ParseRanges(o.S("ranges"), n),
            _ => Enumerable.Range(0, n).Select(i => new List<int> { i }).ToList(),
        };
        Directory.CreateDirectory(outDir);
        var outs = new List<string>();
        for (int gi = 0; gi < groups.Count; gi++)
        {
            ct.ThrowIfCancellationRequested();
            var g = groups[gi];
            using var part = NewDoc(doc);
            foreach (var p in g) part.AddPage(doc.Pages[p]);
            var label = g.Count == 1 ? $"{g[0] + 1}" : $"{g[0] + 1}-{g[^1] + 1}";
            var path = Jobs.Unique(Path.Combine(outDir, $"{baseName}_{label}.pdf"));
            part.Save(path);
            outs.Add(path);
            progress((gi + 1.0) / groups.Count);
        }
        return outs;
    }

    /// <summary>Şifre koyma (AES-256) ya da kaldırma.</summary>
    public static void Protect(string src, string dst, string? currentPassword, string userPw, string ownerPw, bool remove, Opt perms)
    {
        using var doc = currentPassword is { Length: > 0 } ? PdfReader.Open(src, currentPassword, PdfDocumentOpenMode.Modify) : PdfReader.Open(src, PdfDocumentOpenMode.Modify);
        var sh = doc.SecurityHandler;
        if (remove) sh.SetEncryptionToNoneAndResetPasswords();
        else
        {
            sh.SetEncryptionToV5();
            doc.SecuritySettings.UserPassword = userPw;
            doc.SecuritySettings.OwnerPassword = ownerPw.Length > 0 ? ownerPw : userPw;
            doc.SecuritySettings.PermitPrint = perms.B("print", true);
            doc.SecuritySettings.PermitFullQualityPrint = perms.B("print", true);
            doc.SecuritySettings.PermitExtractContent = perms.B("copy", true);
            doc.SecuritySettings.PermitModifyDocument = perms.B("modify", true);
            doc.SecuritySettings.PermitAnnotations = perms.B("modify", true);
        }
        doc.Save(dst);
    }

    /// <summary>Görsellerden PDF. page: a4 | letter | fit (görsel boyutunda); margin mm.</summary>
    public static async Task ImagesToPdfAsync(IReadOnlyList<string> images, string dst, Opt o, string temp, Action<double> progress, CancellationToken ct)
    {
        using var doc = NewDoc();
        string size = o.S("page", "a4");
        double margin = Math.Clamp(o.N("margin", 10), 0, 60) * 72 / 25.4;
        int q = Math.Clamp(o.I("quality", 85), 10, 100);
        for (int i = 0; i < images.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            using var loaded = await Images.LoadAsync(images[i], false, temp, ct);
            var im = loaded.Autorot();
            if (im.Interpretation is NetVips.Enums.Interpretation.Cmyk) im = im.Colourspace(NetVips.Enums.Interpretation.Srgb);
            if (im.Format != NetVips.Enums.BandFormat.Uchar) im = im.Cast(NetVips.Enums.BandFormat.Uchar);
            // Çok büyük fotoğraflar sayfaya 300 dpi yeter: küçült
            int maxPx = o.I("maxPx", 3508);
            if (Math.Max(im.Width, im.Height) > maxPx) im = im.Resize((double)maxPx / Math.Max(im.Width, im.Height), kernel: NetVips.Enums.Kernel.Lanczos3);
            bool alpha = im.HasAlpha();
            var file = Path.Combine(temp, Guid.NewGuid().ToString("N") + (alpha ? ".png" : ".jpg"));
            if (alpha) im.Pngsave(file, compression: 6); else im.Jpegsave(file, q: q, optimizeCoding: true);
            int iw = im.Width, ih = im.Height;
            if (!ReferenceEquals(im, loaded)) im.Dispose();
            try
            {
                var page = doc.AddPage();
                double pw, ph;
                if (size == "fit") { pw = iw * 72.0 / 150 + margin * 2; ph = ih * 72.0 / 150 + margin * 2; }
                else
                {
                    (pw, ph) = size == "letter" ? (612.0, 792.0) : (595.28, 841.89);
                    bool land = o.S("orientation", "auto") switch { "landscape" => true, "portrait" => false, _ => iw > ih };
                    if (land) (pw, ph) = (ph, pw);
                }
                page.Width = XUnit.FromPoint(pw); page.Height = XUnit.FromPoint(ph);
                using var gfx = XGraphics.FromPdfPage(page);
                using var xi = XImage.FromFile(file);
                double bw = pw - margin * 2, bh = ph - margin * 2;
                double s = Math.Min(bw / iw, bh / ih);
                double dw = iw * s, dh = ih * s;
                gfx.DrawImage(xi, margin + (bw - dw) / 2, margin + (bh - dh) / 2, dw, dh);
            }
            finally { try { File.Delete(file); } catch { } }
            progress((i + 1.0) / images.Count);
        }
        doc.Save(dst);
    }

    /// <summary>PDF sayfalarını görsele çevirir. pages: boşsa hepsi, yoksa "1-3,5".</summary>
    public static async Task<List<string>> ToImagesAsync(string src, string outDir, string baseName, string fmt, Opt o, string? password, string temp, Action<double> progress, CancellationToken ct)
    {
        using var doc = Pdfium.Open(src, password);
        var list = o.S("pages") is { Length: > 0 } spec ? ParseRanges(spec, doc.Pages).SelectMany(x => x).Distinct().ToList() : Enumerable.Range(0, doc.Pages).ToList();
        double dpi = Math.Clamp(o.N("dpi", 150), 36, 600);
        Directory.CreateDirectory(outDir);
        var outs = new List<string>();
        int digits = Math.Max(2, doc.Pages.ToString().Length);
        for (int k = 0; k < list.Count; k++)
        {
            ct.ThrowIfCancellationRequested();
            var (bgra, w, h, stride) = doc.Render(list[k], dpi);
            using var raw = Img.NewFromMemory(bgra, w, h, 4, NetVips.Enums.BandFormat.Uchar);
            // BGRA → RGB (zemin beyaz, saydamlık gerekmez)
            using var rgb = raw[2].Bandjoin(raw[1], raw[0]).Copy(interpretation: NetVips.Enums.Interpretation.Srgb);
            var path = Jobs.Unique(Path.Combine(outDir, $"{baseName}_{(list[k] + 1).ToString().PadLeft(digits, '0')}.{Formats.OutExt(fmt)}"));
            await Images.SaveAsync(rgb, path, fmt, o, temp, ct);
            outs.Add(path);
            progress((k + 1.0) / list.Count);
        }
        return outs;
    }

    /// <summary>
    /// Küçültme: içindeki JPEG görselleri yeniden sıkıştırır ve gerekirse küçültür (metin ve vektörler olduğu gibi kalır),
    /// akışları en iyi sıkıştırmayla yeniden yazar. Sonuç büyükse kaynak kopyalanır.
    /// </summary>
    public static void Compress(string src, string dst, Opt o, string? password, Action<double> progress, CancellationToken ct)
    {
        int q = o.S("level", "balanced") switch { "strong" => 50, "light" => 80, _ => 65 };
        int maxPx = o.S("level", "balanced") switch { "strong" => 1400, "light" => 2600, _ => 2000 };
        using (var doc = password is { Length: > 0 } ? PdfReader.Open(src, password, PdfDocumentOpenMode.Modify) : PdfReader.Open(src, PdfDocumentOpenMode.Modify))
        {
            doc.Options.CompressContentStreams = true;
            doc.Options.FlateEncodeMode = PdfFlateEncodeMode.BestCompression;
            var objs = doc.Internals.GetAllObjects();
            int done = 0;
            foreach (var obj in objs)
            {
                ct.ThrowIfCancellationRequested();
                done++;
                if (done % 20 == 0) progress((double)done / objs.Length);
                if (obj is not PdfDictionary d || d.Stream == null) continue;
                if (d.Elements.GetName("/Subtype") != "/Image") continue;
                var filter = d.Elements["/Filter"];
                bool dct = filter is PdfName { Value: "/DCTDecode" } || (filter is PdfArray fa && fa.Elements.Count == 1 && fa.Elements[0] is PdfName { Value: "/DCTDecode" });
                if (!dct) continue;
                var data = d.Stream.Value;
                if (data.Length < 30_000) continue;
                try
                {
                    using var im = Img.JpegloadBuffer(data);
                    if (im.Interpretation is not (NetVips.Enums.Interpretation.Srgb or NetVips.Enums.Interpretation.Bw)) continue; // CMYK vb. dokunma
                    var x = im;
                    if (Math.Max(im.Width, im.Height) > maxPx) x = im.Resize((double)maxPx / Math.Max(im.Width, im.Height), kernel: NetVips.Enums.Kernel.Lanczos3);
                    var buf = x.JpegsaveBuffer(q: q, optimizeCoding: true, keep: NetVips.Enums.ForeignKeep.None);
                    if (buf.Length < data.Length * 0.9)
                    {
                        d.Stream.Value = buf;
                        d.Elements.SetInteger("/Width", x.Width);
                        d.Elements.SetInteger("/Height", x.Height);
                        d.Elements.SetInteger("/Length", buf.Length);
                        d.Elements.Remove("/DecodeParms");
                    }
                    if (!ReferenceEquals(x, im)) x.Dispose();
                }
                catch { /* çözülemeyen görsel olduğu gibi kalır */ }
            }
            doc.Save(dst);
        }
        if (new FileInfo(dst).Length >= new FileInfo(src).Length) File.Copy(src, dst, true);
        progress(1);
    }

    public static (int Pages, bool Encrypted) Info(string path)
    {
        try { using var d = PdfReader.Open(path, PdfDocumentOpenMode.Import); return (d.PageCount, false); }
        catch (PdfReaderException) { return (0, true); }
    }
}
