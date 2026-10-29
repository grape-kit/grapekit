using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace MarkdownEditor;

/// <summary>
/// Belge klasörlerindeki dosyaları (göreli görseller) https://doc.grapekit.local/&lt;anahtar&gt;/… adresinden sunar.
/// Kabuk istekleri kendisi yanıtlar: klasör sonradan eklense de hemen çalışır, klasör dışına çıkılamaz.
/// </summary>
internal static class DocHosts
{
    const string Host = "doc.grapekit.local";
    static readonly Dictionary<string, string> Dirs = new(StringComparer.OrdinalIgnoreCase);

    static string Key(string dir) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(dir.TrimEnd('\\').ToLowerInvariant())))[..16].ToLowerInvariant();

    /// <summary>Klasörün adres kökü (sonunda /).</summary>
    public static string BaseFor(string dir)
    {
        var k = Key(dir);
        lock (Dirs) Dirs[k] = Path.GetFullPath(dir);
        return $"https://{Host}/{k}/";
    }

    public static void Install(CoreWebView2 wv, CoreWebView2Environment env)
    {
        wv.AddWebResourceRequestedFilter($"https://{Host}/*", CoreWebView2WebResourceContext.All);
        wv.WebResourceRequested += (_, e) =>
        {
            if (!Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var u) || u.Host != Host) return;
            var parts = u.AbsolutePath.TrimStart('/').Split('/', 2);
            string? dir;
            lock (Dirs) Dirs.TryGetValue(parts[0], out dir);
            if (dir == null || parts.Length < 2) { e.Response = env.CreateWebResourceResponse(null, 404, "Not Found", ""); return; }
            var rel = Uri.UnescapeDataString(parts[1]).Replace('/', '\\');
            var full = Path.GetFullPath(Path.Combine(dir, rel));
            if (!full.StartsWith(dir.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
            {
                e.Response = env.CreateWebResourceResponse(null, 404, "Not Found", "");
                return;
            }
            try
            {
                var stream = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                e.Response = env.CreateWebResourceResponse(stream, 200, "OK", $"Content-Type: {MimeOf(full)}\r\nAccess-Control-Allow-Origin: *\r\nCache-Control: no-cache");
            }
            catch { e.Response = env.CreateWebResourceResponse(null, 403, "Forbidden", ""); }
        };
    }

    static string MimeOf(string p) => Path.GetExtension(p).ToLowerInvariant() switch
    {
        ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".gif" => "image/gif", ".webp" => "image/webp", ".svg" => "image/svg+xml",
        ".bmp" => "image/bmp", ".avif" => "image/avif", ".ico" => "image/x-icon", ".mp4" => "video/mp4", ".webm" => "video/webm", ".mp3" => "audio/mpeg",
        ".css" => "text/css", ".pdf" => "application/pdf", _ => "application/octet-stream",
    };
}

/// <summary>
/// Görünmeyen ikinci bir WebView2: PDF, PNG, yazdırma ve öğe görüntüsü. İşler sırayla yürür.
/// </summary>
internal sealed class PrintEngine : IDisposable
{
    readonly CoreWebView2Environment _env;
    readonly string _www;
    readonly string _temp = Path.Combine(Path.GetTempPath(), "GrapekitMarkdown", "print");
    readonly SemaphoreSlim _lock = new(1, 1);
    Form? _host;
    WebView2? _view;

    public PrintEngine(CoreWebView2Environment env, string wwwroot) { _env = env; _www = wwwroot; }

    async Task<CoreWebView2> Ready()
    {
        if (_view?.CoreWebView2 != null) return _view.CoreWebView2;
        _host = new HiddenForm
        {
            FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false, StartPosition = FormStartPosition.Manual,
            Location = new Point(-32000, -32000), Size = new Size(1240, 1754), Opacity = 0, Text = "print",
        };
        _view = new WebView2 { Dock = DockStyle.Fill };
        _host.Controls.Add(_view);
        _host.Show();
        await _view.EnsureCoreWebView2Async(_env);
        var wv = _view.CoreWebView2;
        wv.Settings.AreDevToolsEnabled = false;
        wv.Settings.AreDefaultContextMenusEnabled = false;
        wv.Settings.IsScriptEnabled = true;
        Directory.CreateDirectory(_temp);
        wv.SetVirtualHostNameToFolderMapping("app.grapekit.local", _www, CoreWebView2HostResourceAccessKind.Allow);
        wv.SetVirtualHostNameToFolderMapping("print.grapekit.local", _temp, CoreWebView2HostResourceAccessKind.Allow);
        DocHosts.Install(wv, _env);
        return wv;
    }

    /// <summary>HTML'i yükler, yazı tipleri ve görseller hazır olunca döner.</summary>
    async Task<CoreWebView2> Load(string html, IEnumerable<string> docDirs, int width = 1240)
    {
        var wv = await Ready();
        foreach (var d in docDirs) DocHosts.BaseFor(d);
        _host!.Bounds = new Rectangle(-32000, -32000, width, Math.Max(_host.Height, 900));
        var name = Guid.NewGuid().ToString("N") + ".html";
        var file = Path.Combine(_temp, name);
        File.WriteAllText(file, html, new UTF8Encoding(false));
        var tcs = new TaskCompletionSource<bool>();
        void done(object? s, CoreWebView2NavigationCompletedEventArgs e) { wv.NavigationCompleted -= done; tcs.TrySetResult(e.IsSuccess); }
        wv.NavigationCompleted += done;
        wv.Navigate($"https://print.grapekit.local/{name}");
        var ok = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(60));
        if (!ok) throw new InvalidOperationException("Sayfa yüklenemedi");
        await Eval(wv, "Promise.all([document.fonts.ready, ...[...document.images].map(i => i.complete ? 0 : new Promise(r => { i.onload = i.onerror = r; }))]).then(() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r))))");
        _ = Task.Delay(TimeSpan.FromMinutes(2)).ContinueWith(_ => { try { File.Delete(file); } catch { } });
        return wv;
    }

    static async Task<JsonNode?> Cdp(CoreWebView2 wv, string method, object args)
    {
        var r = await wv.CallDevToolsProtocolMethodAsync(method, JsonSerializer.Serialize(args));
        return JsonNode.Parse(r);
    }

    static async Task<JsonNode?> Eval(CoreWebView2 wv, string expr)
    {
        var r = await Cdp(wv, "Runtime.evaluate", new { expression = expr, awaitPromise = true, returnByValue = true });
        return r?["result"]?["value"];
    }

    async Task<T> Job<T>(Func<Task<T>> work)
    {
        await _lock.WaitAsync();
        try { return await work(); } finally { _lock.Release(); }
    }

    /// <summary>HTML → PDF dosyası. options: Page.printToPDF parametreleri.</summary>
    public Task<bool> Pdf(string html, JsonObject options, string path, IEnumerable<string> docDirs) => Job(async () =>
    {
        var wv = await Load(html, docDirs);
        var opt = JsonNode.Parse(options.ToJsonString())!.AsObject();
        opt["transferMode"] = "ReturnAsBase64";
        var heads = (await Eval(wv, "[...document.querySelectorAll('h1,h2,h3,h4,h5,h6')].map(h => h.textContent.replace(/\\s+/g, ' ').trim())"))?.AsArray()
            .Select(x => x?.GetValue<string>() ?? "").ToHashSet() ?? [];
        var r = await wv.CallDevToolsProtocolMethodAsync("Page.printToPDF", opt.ToJsonString());
        var data = JsonNode.Parse(r)?["data"]?.GetValue<string>() ?? throw new InvalidOperationException("PDF üretilemedi");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var bytes = Convert.FromBase64String(data);
        PdfOutline.FixDoubledTitles(bytes, heads);
        await File.WriteAllBytesAsync(path, bytes);
        return true;
    });

    /// <summary>HTML → PNG (bütün sayfa).</summary>
    public Task<bool> Png(string html, string path, int width, double scale, IEnumerable<string> docDirs) => Job(async () =>
    {
        var wv = await Load(html, docDirs, width);
        await Cdp(wv, "Emulation.setDeviceMetricsOverride", new { width, height = 900, deviceScaleFactor = scale, mobile = false });
        var h = (await Eval(wv, "Math.ceil(document.documentElement.scrollHeight)"))?.GetValue<double>() ?? 900;
        // Chromium dokusu ~16384 px ile sınırlı: çok uzun belgede ölçeği düşür
        double s = Math.Min(scale, 16000.0 / Math.Max(1, h));
        await Cdp(wv, "Emulation.setDeviceMetricsOverride", new { width, height = (int)h, deviceScaleFactor = s, mobile = false });
        var r = await Cdp(wv, "Page.captureScreenshot", new { format = "png", captureBeyondViewport = true, fromSurface = true, clip = new { x = 0, y = 0, width, height = h, scale = 1 } });
        await Cdp(wv, "Emulation.clearDeviceMetricsOverride", new { });
        var data = r?["data"]?.GetValue<string>() ?? throw new InvalidOperationException("Görsel üretilemedi");
        await File.WriteAllBytesAsync(path, Convert.FromBase64String(data));
        return true;
    });

    /// <summary>#cap0…#capN öğelerinin görüntüleri (Word'e formül/diyagram için).</summary>
    public Task<JsonArray> Capture(string html, int count) => Job(async () =>
    {
        var wv = await Load(html, []);
        const double dpr = 2;
        await Cdp(wv, "Emulation.setDeviceMetricsOverride", new { width = 1000, height = 900, deviceScaleFactor = dpr, mobile = false });
        var rects = await Eval(wv, $"Array.from({{length:{count}}},(_, i)=>{{const e=document.getElementById('cap'+i);if(!e)return null;const r=e.getBoundingClientRect();return [r.left+scrollX,r.top+scrollY,r.width,r.height]}})");
        var arr = new JsonArray();
        var list = rects?.AsArray() ?? [];
        foreach (var r in list)
        {
            if (r is null) { arr.Add(null); continue; }
            double x = r[0]!.GetValue<double>(), y = r[1]!.GetValue<double>(), w = Math.Max(1, r[2]!.GetValue<double>()), h = Math.Max(1, r[3]!.GetValue<double>());
            var shot = await Cdp(wv, "Page.captureScreenshot", new { format = "png", captureBeyondViewport = true, clip = new { x, y, width = w, height = h, scale = 1 } });
            arr.Add(new JsonObject { ["data"] = shot?["data"]?.GetValue<string>(), ["w"] = (int)Math.Round(w * dpr), ["h"] = (int)Math.Round(h * dpr), ["dpr"] = dpr });
        }
        await Cdp(wv, "Emulation.clearDeviceMetricsOverride", new { });
        return arr;
    });

    /// <summary>Windows yazdırma penceresini açar.</summary>
    /// <summary>Sistem yazdırma diyaloğu. near: diyaloğun ortalanacağı alan (ana pencere).</summary>
    public Task<bool> Print(string html, IEnumerable<string> docDirs, Rectangle near) => Job(async () =>
    {
        var wv = await Load(html, docDirs);
        // Diyalog sahibinin konumuna göre açılır: gizli pencereyi (görünmez, 1×1) ana pencerenin ortasına al.
        // Sonraki iş Load ile onu yine ekran dışına taşır.
        _host!.Bounds = new Rectangle(near.Left + near.Width / 2, near.Top + near.Height / 2, 1, 1);
        wv.ShowPrintUI(CoreWebView2PrintDialogKind.System);
        return true;
    });

    public void Dispose() { _view?.Dispose(); _host?.Dispose(); try { Directory.Delete(_temp, true); } catch { } }
}

/// <summary>
/// Chromium'un etiketli PDF yer imlerinde sayfa başına düşen başlığın metni iki kez yazılıyor ("BirBir").
/// Yer imi başlığı, belgedeki bir başlığın iki katıysa teke indirilir. Dosyadaki bayt konumları değişmesin diye
/// kısalan yer boşlukla doldurulur (çapraz başvuru tablosu geçerli kalır).
/// </summary>
internal static class PdfOutline
{
    public static void FixDoubledTitles(byte[] pdf, IReadOnlySet<string> headings)
    {
        if (headings.Count == 0) return;
        var key = "/Title"u8;
        for (int i = pdf.AsSpan().IndexOf(key); i >= 0;)
        {
            int s = i + key.Length;
            while (s < pdf.Length && pdf[s] is (byte)' ' or (byte)'\r' or (byte)'\n') s++;
            if (s < pdf.Length && pdf[s] == '(') FixLiteral(pdf, s, headings);
            else if (s < pdf.Length && pdf[s] == '<') FixHex(pdf, s, headings);
            int next = pdf.AsSpan(s).IndexOf(key);
            i = next < 0 ? -1 : s + next;
        }
    }

    // (metin): kaçış dizileri iki yarıda da aynı olduğundan ham baytlar karşılaştırılır
    static void FixLiteral(byte[] pdf, int start, IReadOnlySet<string> heads)
    {
        int end = start + 1;
        for (; end < pdf.Length && pdf[end] != ')'; end++) if (pdf[end] == '\\') end++;
        if (end >= pdf.Length) return;
        var inner = pdf.AsSpan(start + 1, end - start - 1);
        if (inner.Length < 2 || inner.Length % 2 != 0 || !inner[..(inner.Length / 2)].SequenceEqual(inner[(inner.Length / 2)..])) return;
        var half = Encoding.Latin1.GetString(inner[..(inner.Length / 2)]).Replace("\\(", "(").Replace("\\)", ")").Replace("\\\\", "\\");
        if (!heads.Contains(half)) return;
        int cut = start + 1 + inner.Length / 2;
        pdf[cut] = (byte)')';
        pdf.AsSpan(cut + 1, end - cut).Fill((byte)' ');
    }

    // <FEFF....>: UTF-16BE
    static void FixHex(byte[] pdf, int start, IReadOnlySet<string> heads)
    {
        int end = pdf.AsSpan(start).IndexOf((byte)'>');
        if (end < 0) return;
        end += start;
        var hex = Encoding.ASCII.GetString(pdf, start + 1, end - start - 1);
        if (!hex.StartsWith("FEFF", StringComparison.OrdinalIgnoreCase) || hex.Any(char.IsWhiteSpace)) return;
        var body = hex[4..];
        if (body.Length < 8 || body.Length % 8 != 0 || body[..(body.Length / 2)] != body[(body.Length / 2)..]) return;
        string half;
        try { half = Encoding.BigEndianUnicode.GetString(Convert.FromHexString(body[..(body.Length / 2)])); } catch { return; }
        if (!heads.Contains(half)) return;
        int cut = start + 1 + 4 + body.Length / 2;
        pdf[cut] = (byte)'>';
        pdf.AsSpan(cut + 1, end - cut).Fill((byte)' ');
    }
}

/// <summary>Görev çubuğunda, Alt+Tab'da görünmeyen ve odak almayan pencere (yazdırma motoru için).</summary>
internal sealed class HiddenForm : Form
{
    protected override CreateParams CreateParams
    {
        get { var cp = base.CreateParams; cp.ExStyle |= 0x80 /* TOOLWINDOW */ | 0x08000000 /* NOACTIVATE */; return cp; }
    }
    protected override bool ShowWithoutActivation => true;
}

/// <summary>PDF önizleme penceresi (Edge'in PDF görüntüleyicisiyle).</summary>
internal sealed class PdfViewer : Form
{
    public PdfViewer(CoreWebView2Environment env, string path, string title, Form owner)
    {
        Text = title; Icon = AppIcon.Get(32); StartPosition = FormStartPosition.CenterParent;
        var b = owner.Bounds;
        Size = new Size((int)(b.Width * 0.8), (int)(b.Height * 0.9));
        var v = new WebView2 { Dock = DockStyle.Fill };
        Controls.Add(v);
        Shown += async (_, _) =>
        {
            await v.EnsureCoreWebView2Async(env);
            v.CoreWebView2.Settings.AreDevToolsEnabled = false;
            v.CoreWebView2.Navigate(new Uri(path).AbsoluteUri);
        };
    }
}
