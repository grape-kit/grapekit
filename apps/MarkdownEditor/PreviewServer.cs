using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace MarkdownEditor;

/// <summary>
/// Dosya Gezgini önizleme bölmesi. Gezgin'e DLL yüklenmez: aynı exe "-Embedding" ile ayrı bir COM sunucusu
/// (LocalServer32) olarak açılır, önizlemeyi Gezgin'in verdiği pencerenin içine WebView2 ile çizer.
/// Bir sorun olursa yalnızca bu işlem etkilenir, Gezgin değil. Boşta kalınca kendiliğinden kapanır.
/// </summary>
public static class PreviewServer
{
    public static readonly Guid Clsid = new("4D2A9E61-8B3C-4F5D-A1E7-3C0DEDA7A0B1");

    static int _locks;
    static Task<CoreWebView2Environment>? _env;
    static readonly HashSet<MarkdownPreview> Live = [];

    public static int Run()
    {
        ApplicationConfiguration.Initialize();
        ErrorLog.Install();
        using var ctx = new ApplicationContext();
        int hr = CoRegisterClassObject(Clsid, new Factory(), 4 /* CLSCTX_LOCAL_SERVER */, 1 /* REGCLS_MULTIPLEUSE */, out uint cookie);
        if (hr != 0) return hr;
        // Bakım: sahibi kapanmış (Gezgin çökmüş/yeniden başlamış) önizlemeleri kapat; uzun süre boş kalınca çık
        int idleTicks = 0;
        var timer = new System.Windows.Forms.Timer { Interval = 30_000 };
        timer.Tick += (_, _) =>
        {
            foreach (var p in Live.ToList()) if (!p.OwnerAlive) p.Unload();
            idleTicks = Live.Count == 0 && _locks <= 0 ? idleTicks + 1 : 0;
            if (idleTicks >= 3) ctx.ExitThread();
        };
        timer.Start();
        Application.Run(ctx);
        CoRevokeClassObject(cookie);
        return 0;
    }

    public static void Enter(MarkdownPreview p) => Live.Add(p);
    public static void Leave(MarkdownPreview p) => Live.Remove(p);

    public static Task<CoreWebView2Environment> Env() => _env ??= CreateEnv();

    static async Task<CoreWebView2Environment> CreateEnv()
    {
        // Ana pencereden ayrı veri klasörü: farklı tarayıcı ayarlarıyla aynı anda çalışabilsin
        var data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Grapekit", GrapekitApp.Exe, "PreviewWebView2");
        var opts = new CoreWebView2EnvironmentOptions("--disable-features=msSmartScreenProtection --enable-features=OverlayScrollbar")
        {
            Language = GrapekitApp.Lang == 0 ? "tr-TR" : "en-US",
            AreBrowserExtensionsEnabled = false,
        };
        return await CoreWebView2Environment.CreateAsync(null, data, opts);
    }

    [DllImport("ole32.dll")] static extern int CoRegisterClassObject([MarshalAs(UnmanagedType.LPStruct)] Guid clsid, [MarshalAs(UnmanagedType.IUnknown)] object unk, uint ctx, uint flags, out uint cookie);
    [DllImport("ole32.dll")] static extern int CoRevokeClassObject(uint cookie);

    [ComVisible(true)]
    public sealed class Factory : IClassFactory, ICustomQueryInterface
    {
        public CustomQueryInterfaceResult GetInterface(ref Guid iid, out IntPtr ppv) => StaOnly.Get(ref iid, out ppv);
        public int CreateInstance(IntPtr outer, ref Guid riid, out IntPtr ppv)
        {
            ppv = IntPtr.Zero;
            if (outer != IntPtr.Zero) return unchecked((int)0x80040110); // CLASS_E_NOAGGREGATION
            var unk = Marshal.GetIUnknownForObject(new MarkdownPreview());
            try { return Marshal.QueryInterface(unk, in riid, out ppv); }
            finally { Marshal.Release(unk); }
        }
        public int LockServer(bool fLock) { _locks += fLock ? 1 : -1; return 0; }
    }
}

/// <summary>Bir önizleme: Gezgin dosyayı verir, pencereyi ve boyutu bildirir, DoPreview ile çizdirir.</summary>
[ComVisible(true)]
public sealed class MarkdownPreview : IPreviewHandler, IInitializeWithFile, IObjectWithSite, IOleWindow, IPreviewHandlerVisuals, ICustomQueryInterface
{
    public CustomQueryInterfaceResult GetInterface(ref Guid iid, out IntPtr ppv) => StaOnly.Get(ref iid, out ppv);
    const long MaxBytes = 4L * 1024 * 1024; // önizleme için yeterli; daha büyükse baştan bu kadarı gösterilir

    string? _path;
    IntPtr _parent;
    Rectangle _bounds;
    object? _site;
    HostControl? _host;
    WebView2? _view;
    bool? _darkFromPane;

    internal bool OwnerAlive => _parent != IntPtr.Zero && IsWindow(_parent);

    // ---------- IInitializeWithFile ----------
    public void Initialize(string pszFilePath, uint grfMode) => _path = pszFilePath;

    // ---------- IPreviewHandler ----------
    public void SetWindow(IntPtr hwnd, ref RECT rect)
    {
        _parent = hwnd;
        _bounds = rect.ToRectangle();
        if (_host is { IsHandleCreated: true }) { SetParent(_host.Handle, _parent); _host.Bounds = _bounds; }
    }

    public void SetRect(ref RECT rect)
    {
        _bounds = rect.ToRectangle();
        if (_host is { IsHandleCreated: true }) _host.Bounds = _bounds;
    }

    public void DoPreview()
    {
        if (_path == null || _parent == IntPtr.Zero) return;
        PreviewServer.Enter(this);
        if (_host == null)
        {
            _host = new HostControl(_parent) { Bounds = _bounds };
            _host.CreateControl();
            ShowWindow(_host.Handle, 5 /* SW_SHOW */);
        }
        _ = LoadAsync(_path);
    }

    public void Unload()
    {
        try { _view?.Dispose(); } catch { }
        try { _host?.Dispose(); } catch { }
        _view = null; _host = null; _path = null;
        PreviewServer.Leave(this);
    }

    public void SetFocus() { try { _view?.Focus(); } catch { } }
    public void QueryFocus(out IntPtr phwnd) => phwnd = GetFocus();
    public int TranslateAccelerator(ref MSG pmsg) => 1; // S_FALSE: Gezgin kendisi işlesin

    // ---------- IOleWindow ----------
    public void GetWindow(out IntPtr phwnd) => phwnd = _host?.Handle ?? _parent;
    public void ContextSensitiveHelp(bool fEnterMode) { }

    // ---------- IObjectWithSite ----------
    public void SetSite(object? pUnkSite) => _site = pUnkSite;
    public void GetSite(ref Guid riid, out IntPtr ppvSite)
    {
        ppvSite = IntPtr.Zero;
        if (_site == null) Marshal.ThrowExceptionForHR(unchecked((int)0x80004005));
        var unk = Marshal.GetIUnknownForObject(_site!);
        try { Marshal.ThrowExceptionForHR(Marshal.QueryInterface(unk, in riid, out ppvSite)); }
        finally { Marshal.Release(unk); }
    }

    // ---------- IPreviewHandlerVisuals: Gezgin'in renkleri (koyu tema) ----------
    public void SetBackgroundColor(uint color)
    {
        int r = (int)(color & 0xFF), g = (int)((color >> 8) & 0xFF), b = (int)((color >> 16) & 0xFF);
        _darkFromPane = (r * 299 + g * 587 + b * 114) / 1000 < 128;
        if (_host != null) _host.BackColor = Color.FromArgb(r, g, b);
    }
    public void SetFont(ref LOGFONTW plf) { }
    public void SetTextColor(uint color) { }

    // ---------- Çizim ----------
    async Task LoadAsync(string path)
    {
        try
        {
            var host = _host;
            if (host == null) return;
            if (_view == null)
            {
                var env = await PreviewServer.Env();
                if (_host != host) return; // bu arada kapatıldı
                _view = new WebView2 { Dock = DockStyle.Fill, DefaultBackgroundColor = host.BackColor };
                host.Controls.Add(_view);
                await _view.EnsureCoreWebView2Async(env);
                var wv = _view.CoreWebView2;
                var s = wv.Settings;
                s.AreDevToolsEnabled = false; s.AreDefaultContextMenusEnabled = false; s.IsStatusBarEnabled = false;
                s.AreBrowserAcceleratorKeysEnabled = false; s.IsZoomControlEnabled = false; s.AreHostObjectsAllowed = false;
                s.IsGeneralAutofillEnabled = false; s.IsPasswordAutosaveEnabled = false; s.IsSwipeNavigationEnabled = false;
                wv.SetVirtualHostNameToFolderMapping("app.grapekit.local", Path.Combine(AppContext.BaseDirectory, "wwwroot"), CoreWebView2HostResourceAccessKind.Allow);
                DocHosts.Install(wv, env);
                // Sayfa dışına gezinme yok; bağlantılar varsayılan tarayıcıda
                wv.NavigationStarting += (_, e) => { if (!e.Uri.StartsWith("https://app.grapekit.local/", StringComparison.OrdinalIgnoreCase)) e.Cancel = true; };
                wv.NewWindowRequested += (_, e) => e.Handled = true;
                wv.WebMessageReceived += (_, e) => OnMessage(e.WebMessageAsJson);
                wv.Navigate("https://app.grapekit.local/peek.html");
            }
            else Post();
        }
        catch (Exception ex) { ErrorLog.Write(ex); }
    }

    void OnMessage(string json)
    {
        try
        {
            var m = JsonNode.Parse(json);
            if (m?["ready"] != null) { Post(); return; }
            var url = m?["open"]?.GetValue<string>();
            if (url != null && Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme is "http" or "https" or "mailto"))
                Process.Start(new ProcessStartInfo(u.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex) { ErrorLog.Write(ex); }
    }

    void Post()
    {
        var wv = _view?.CoreWebView2;
        if (wv == null || _path == null) return;
        string text;
        try
        {
            var fi = new FileInfo(_path);
            text = fi.Length > MaxBytes ? ReadHead(_path) : TextFiles.Read(_path).Text;
        }
        catch (Exception ex) { text = "> " + ex.Message; }
        var settings = Store.LoadSettings();
        var dir = Path.GetDirectoryName(_path);
        var msg = new JsonObject
        {
            ["text"] = text,
            ["docBase"] = dir != null ? DocHosts.BaseFor(dir) : "",
            ["dark"] = _darkFromPane ?? !ThemeInfo.AppsUseLight(),
            ["lang"] = GrapekitApp.Lang == 0 ? "tr" : "en",
            ["settings"] = new JsonObject
            {
                ["previewTheme"] = settings?["previewTheme"]?.DeepClone(),
                ["previewFontSize"] = settings?["previewFontSize"]?.DeepClone(),
                ["lineHeight"] = settings?["lineHeight"]?.DeepClone(),
                ["breaks"] = settings?["breaks"]?.DeepClone(),
                ["remoteImages"] = settings?["remoteImages"]?.DeepClone() ?? true,
                ["previewWidth"] = 0, // dar bölmede tam genişlik
            },
        };
        wv.PostWebMessageAsJson(msg.ToJsonString());
    }

    static string ReadHead(string path)
    {
        using var f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var buf = new byte[MaxBytes];
        int n = f.Read(buf, 0, buf.Length);
        return new System.Text.UTF8Encoding(false).GetString(buf, 0, n);
    }

    [DllImport("user32.dll")] static extern IntPtr SetParent(IntPtr child, IntPtr parent);
    [DllImport("user32.dll")] static extern IntPtr GetFocus();
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);

    /// <summary>Gezgin'in önizleme penceresinin doğrudan çocuğu olarak oluşturulan kap.</summary>
    sealed class HostControl(IntPtr parent) : Control
    {
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.Parent = parent;
                cp.Style = 0x40000000 /* WS_CHILD */ | 0x02000000 /* WS_CLIPCHILDREN */ | 0x04000000 /* WS_CLIPSIBLINGS */;
                return cp;
            }
        }
    }
}

/// <summary>
/// .NET'in COM sarmalayıcıları IMarshal (serbest iş parçacıklı) sunar; o zaman Gezgin'in çağrıları COM işçi
/// iş parçacıklarına düşer. IMarshal reddedilince standart sıralama kullanılır ve her çağrı nesnenin oluşturulduğu
/// ana (STA, mesaj döngülü) iş parçacığına gelir: WebView2 ve pencereler orada çalışır.
/// </summary>
internal static class StaOnly
{
    static readonly Guid IMarshal = new("00000003-0000-0000-C000-000000000046");
    public static CustomQueryInterfaceResult Get(ref Guid iid, out IntPtr ppv)
    {
        ppv = IntPtr.Zero;
        return iid == IMarshal ? CustomQueryInterfaceResult.Failed : CustomQueryInterfaceResult.NotHandled;
    }
}

// ---------- COM arayüzleri (Windows kabuğu) ----------
[StructLayout(LayoutKind.Sequential)]
public struct RECT
{
    public int Left, Top, Right, Bottom;
    public readonly Rectangle ToRectangle() => Rectangle.FromLTRB(Left, Top, Right, Bottom);
}

[StructLayout(LayoutKind.Sequential)]
public struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public int ptX, ptY; }

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct LOGFONTW
{
    public int lfHeight, lfWidth, lfEscapement, lfOrientation, lfWeight;
    public byte lfItalic, lfUnderline, lfStrikeOut, lfCharSet, lfOutPrecision, lfClipPrecision, lfQuality, lfPitchAndFamily;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string lfFaceName;
}

[ComImport, Guid("00000001-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), ComVisible(true)]
public interface IClassFactory
{
    [PreserveSig] int CreateInstance(IntPtr pUnkOuter, ref Guid riid, out IntPtr ppvObject);
    [PreserveSig] int LockServer(bool fLock);
}

[ComImport, Guid("8895b1c6-b41f-4c1c-a562-0d564250836f"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), ComVisible(true)]
public interface IPreviewHandler
{
    void SetWindow(IntPtr hwnd, ref RECT prc);
    void SetRect(ref RECT prc);
    void DoPreview();
    void Unload();
    void SetFocus();
    void QueryFocus(out IntPtr phwnd);
    [PreserveSig] int TranslateAccelerator(ref MSG pmsg);
}

[ComImport, Guid("b7d14566-0509-4cce-a71f-0a554233bd9b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), ComVisible(true)]
public interface IInitializeWithFile
{
    void Initialize([MarshalAs(UnmanagedType.LPWStr)] string pszFilePath, uint grfMode);
}

[ComImport, Guid("FC4801A3-2BA9-11CF-A229-00AA003D7352"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), ComVisible(true)]
public interface IObjectWithSite
{
    void SetSite([MarshalAs(UnmanagedType.IUnknown)] object? pUnkSite);
    void GetSite(ref Guid riid, out IntPtr ppvSite);
}

[ComImport, Guid("00000114-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), ComVisible(true)]
public interface IOleWindow
{
    void GetWindow(out IntPtr phwnd);
    void ContextSensitiveHelp([MarshalAs(UnmanagedType.Bool)] bool fEnterMode);
}

[ComImport, Guid("196bf9a5-b346-4ef0-aa1e-5dcdb76768b1"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), ComVisible(true)]
public interface IPreviewHandlerVisuals
{
    void SetBackgroundColor(uint color);
    void SetFont(ref LOGFONTW plf);
    void SetTextColor(uint color);
}
