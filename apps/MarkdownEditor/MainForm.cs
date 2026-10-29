using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Win32;

namespace MarkdownEditor;

/// <summary>
/// Ana pencere: Windows çerçevesi korunur (yeniden boyutlandırma, gölge, yerleştirme), başlık çubuğu arayüzde çizilir.
/// İçerik tamamen WebView2'dir; kabukla iletişim <see cref="HandleAsync"/> üzerinden.
/// </summary>
internal sealed partial class MainForm : Form
{
    readonly WebView2 _web = new() { DefaultBackgroundColor = Color.FromArgb(0x1A, 0x0B, 0x33) };
    readonly string _www = Path.Combine(AppContext.BaseDirectory, "wwwroot");
    readonly List<string> _startupFiles;
    CoreWebView2Environment? _env;
    PrintEngine? _print;
    bool _allowClose, _ready, _fullscreen;
    FormWindowState _preFullState;
    Rectangle _preFullBounds;
    Color _edge = Color.FromArgb(0x1A, 0x0B, 0x33);
    readonly TaskCompletionSource _readyTcs = new();
    public bool Headless { get; init; }

    public MainForm(List<string> files)
    {
        _startupFiles = files;
        Text = GrapekitApp.Name;
        Icon = AppIcon.Get(32);
        AutoScaleMode = AutoScaleMode.None;
        BackColor = _edge;
        MinimumSize = new Size(640, 420);
        StartPosition = FormStartPosition.Manual;
        KeyPreview = false;
        var w = Store.LoadWindow();
        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1366, 768);
        if (w != null && Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(new Rectangle(w.X, w.Y, w.W, w.H))))
        {
            Bounds = new Rectangle(w.X, w.Y, Math.Max(640, w.W), Math.Max(420, w.H));
            if (w.Max) WindowState = FormWindowState.Maximized;
        }
        else
        {
            var size = new Size(Math.Min(1440, area.Width - 80), Math.Min(920, area.Height - 60));
            Bounds = new Rectangle(area.X + (area.Width - size.Width) / 2, area.Y + (area.Height - size.Height) / 2, size.Width, size.Height);
        }
        Controls.Add(_web);
        LayoutWeb();
        SystemEvents.UserPreferenceChanged += OnPreference;
    }

    // ===================== Pencere çerçevesi =====================
    const int WM_NCCALCSIZE = 0x83, WM_NCHITTEST = 0x84, HTCLIENT = 1, HTTOP = 12, HTTOPLEFT = 13, HTTOPRIGHT = 14;

    int TopInset => WindowState == FormWindowState.Maximized || _fullscreen ? 0 : Math.Max(1, (int)Math.Round(4 * DeviceDpi / 96.0));

    void LayoutWeb() => _web.SetBounds(0, TopInset, ClientSize.Width, Math.Max(0, ClientSize.Height - TopInset));

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_NCCALCSIZE && m.WParam != IntPtr.Zero && !_fullscreen)
        {
            // Başlık çubuğunu kaldır; sol, sağ ve alttaki (görünmez) yeniden boyutlandırma kenarları kalsın
            var p = System.Runtime.InteropServices.Marshal.PtrToStructure<Native.NCCALCSIZE_PARAMS>(m.LParam);
            int top = p.R0.Top;
            base.WndProc(ref m);
            p = System.Runtime.InteropServices.Marshal.PtrToStructure<Native.NCCALCSIZE_PARAMS>(m.LParam);
            uint dpi = Native.GetDpiForWindow(Handle);
            int frame = Native.GetSystemMetricsForDpi(Native.SM_CYFRAME, dpi) + Native.GetSystemMetricsForDpi(Native.SM_CXPADDEDBORDER, dpi);
            p.R0.Top = top + (Native.IsZoomed(Handle) ? frame : 0);
            System.Runtime.InteropServices.Marshal.StructureToPtr(p, m.LParam, false);
            m.Result = IntPtr.Zero;
            return;
        }
        base.WndProc(ref m);
        if (m.Msg == WM_NCHITTEST && (int)m.Result == HTCLIENT && WindowState == FormWindowState.Normal && !_fullscreen)
        {
            long lp = m.LParam.ToInt64();
            var pt = PointToClient(new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF)));
            int edge = TopInset;
            if (pt.Y < edge) m.Result = pt.X < 12 ? HTTOPLEFT : pt.X > ClientSize.Width - 12 ? HTTOPRIGHT : HTTOP;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // Çerçeveyi yeniden hesaplat (başlık kalksın)
        SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0, 0x0027 /* NOSIZE|NOMOVE|NOZORDER|FRAMECHANGED */);
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutWeb();
        if (_ready) Emit("window-state", new JsonObject { ["maximized"] = WindowState == FormWindowState.Maximized, ["fullscreen"] = _fullscreen });
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e) { base.OnDpiChanged(e); LayoutWeb(); }

    void SetFullscreen(bool? on)
    {
        bool want = on ?? !_fullscreen;
        if (want == _fullscreen) return;
        if (want)
        {
            _preFullState = WindowState; _preFullBounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            _fullscreen = true;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Normal;
            Bounds = Screen.FromControl(this).Bounds;
        }
        else
        {
            _fullscreen = false;
            FormBorderStyle = FormBorderStyle.Sizable;
            Bounds = _preFullBounds;
            WindowState = _preFullState;
            SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0, 0x0027);
        }
        LayoutWeb();
        Emit("window-state", new JsonObject { ["maximized"] = WindowState == FormWindowState.Maximized, ["fullscreen"] = _fullscreen });
    }

    // ===================== WebView2 =====================
    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        try
        {
            var data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Grapekit", GrapekitApp.Exe, "WebView2");
            var browserArgs = "--disable-features=msSmartScreenProtection --enable-features=OverlayScrollbar";
#if DEBUG
            // Yalnızca geliştirme derlemesi: otomatik testler için uzaktan hata ayıklama kapısı
            if (Environment.GetEnvironmentVariable("GRAPEKIT_CDP_PORT") is { Length: > 0 } port) browserArgs += " --remote-debugging-port=" + port;
#endif
            var opts = new CoreWebView2EnvironmentOptions(additionalBrowserArguments: browserArgs)
            {
                Language = GrapekitApp.Lang == 0 ? "tr-TR" : "en-US",
                AreBrowserExtensionsEnabled = false,
            };
            _env = await CoreWebView2Environment.CreateAsync(null, data, opts);
            await _web.EnsureCoreWebView2Async(_env);
        }
        catch (WebView2RuntimeNotFoundException)
        {
            if (MessageBox.Show(this, GrapekitApp.Lang == 0
                    ? "Markdown Editörü, Windows'un Microsoft Edge WebView2 bileşenine ihtiyaç duyar. Şimdi indirme sayfası açılsın mı?"
                    : "Markdown Studio needs the Microsoft Edge WebView2 component. Open the download page now?",
                    GrapekitApp.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                Brand.Open("https://go.microsoft.com/fwlink/p/?LinkId=2124703");
            _allowClose = true; Close(); return;
        }

        var wv = _web.CoreWebView2;
        var s = wv.Settings;
#if DEBUG
        s.AreDevToolsEnabled = true;
#else
        s.AreDevToolsEnabled = false;
#endif
        s.AreBrowserAcceleratorKeysEnabled = false;   // Ctrl+P, F5, Ctrl+F gibi tarayıcı kısayolları kapalı
        s.IsZoomControlEnabled = false;               // yakınlaştırmayı uygulama yönetir
        s.IsStatusBarEnabled = false;
        s.IsPasswordAutosaveEnabled = false;
        s.IsGeneralAutofillEnabled = false;
        s.IsSwipeNavigationEnabled = false;
        s.IsPinchZoomEnabled = false;
        s.AreHostObjectsAllowed = false;
        s.IsNonClientRegionSupportEnabled = true;    // CSS app-region: drag → pencere sürükleme
        wv.SetVirtualHostNameToFolderMapping("app.grapekit.local", _www, CoreWebView2HostResourceAccessKind.Allow);
        DocHosts.Install(wv, _env);
        wv.WebMessageReceived += OnMessage;
        wv.ContextMenuRequested += OnContextMenu;
        wv.NewWindowRequested += (_, a) => { a.Handled = true; if (Uri.TryCreate(a.Uri, UriKind.Absolute, out var u) && (u.Scheme == "https" || u.Scheme == "http" || u.Scheme == "mailto") && !u.Host.EndsWith(".grapekit.local")) Brand.Open(a.Uri.Replace("http://", "https://")); };
        wv.NavigationStarting += (_, a) =>
        {
            // Yalnızca uygulamanın kendi sayfası yüklenebilir
            if (!a.Uri.StartsWith("https://app.grapekit.local/", StringComparison.OrdinalIgnoreCase)) { a.Cancel = true; if (a.Uri.StartsWith("https://")) Brand.Open(a.Uri); }
        };
        wv.PermissionRequested += (_, a) =>
        {
            a.State = a.PermissionKind is CoreWebView2PermissionKind.ClipboardRead ? CoreWebView2PermissionState.Allow : CoreWebView2PermissionState.Deny;
        };
        wv.ProcessFailed += (_, a) =>
        {
            ErrorLog.Write(new Exception($"WebView2 işlem hatası: {a.ProcessFailedKind} {a.Reason}"));
            if (a.ProcessFailedKind == CoreWebView2ProcessFailedKind.RenderProcessExited) wv.Reload();
        };
        _print = new PrintEngine(_env, _www);
        wv.Navigate("https://app.grapekit.local/index.html");
    }

    void OnContextMenu(object? sender, CoreWebView2ContextMenuRequestedEventArgs e)
    {
        // Yalnızca yazım önerileri ve düzenleme komutları kalsın
        var keep = new HashSet<string> { "spellCheck", "cut", "copy", "paste", "pasteAndMatchStyle", "selectAll", "undo", "redo", "addToDictionary", "other" };
        var items = e.MenuItems;
        for (int i = items.Count - 1; i >= 0; i--)
        {
            var it = items[i];
            bool spell = it.Name.StartsWith("spellCheck", StringComparison.OrdinalIgnoreCase) || it.Name.Contains("Dictionary", StringComparison.OrdinalIgnoreCase);
            if (!spell && !keep.Contains(it.Name) && it.Kind != CoreWebView2ContextMenuItemKind.Separator) items.RemoveAt(i);
        }
        // Baştaki/sondaki/art arda ayraçları temizle
        for (int i = items.Count - 1; i >= 0; i--)
            if (items[i].Kind == CoreWebView2ContextMenuItemKind.Separator && (i == 0 || i == items.Count - 1 || items[i - 1].Kind == CoreWebView2ContextMenuItemKind.Separator)) items.RemoveAt(i);
        if (items.Count == 0) e.Handled = true;
    }

    // ===================== Mesajlaşma =====================
    async void OnMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        JsonNode? msg;
        try { msg = JsonNode.Parse(e.TryGetWebMessageAsString()); } catch { return; }
        if (msg is null) return;
        var id = msg["id"];
        var method = msg["method"]?.GetValue<string>() ?? "";
        var p = msg["params"] as JsonObject ?? [];
        // Bırakılan dosyaların yolları ek nesne olarak gelir
        List<string>? dropped = null;
        if (e.AdditionalObjects is { Count: > 0 } extra)
            dropped = extra.OfType<CoreWebView2File>().Select(f => f.Path).ToList();
        JsonObject reply = new() { ["id"] = id?.DeepClone() };
        try { reply["result"] = await HandleAsync(method, p, dropped); }
        catch (Exception ex)
        {
            if (ex is not BridgeException && ex is not IOException && ex is not UnauthorizedAccessException) ErrorLog.Write(ex);
            reply["error"] = new JsonObject { ["message"] = ex.Message, ["code"] = ex is BridgeException b ? b.Code : ex.GetType().Name };
        }
        if (id != null) Post(reply);
    }

    void Post(JsonNode n)
    {
        if (IsDisposed || _web.CoreWebView2 == null) return;
        _web.CoreWebView2.PostWebMessageAsJson(n.ToJsonString());
    }

    /// <summary>Arayüze olay gönderir.</summary>
    public void Emit(string ev, JsonNode? data = null)
    {
        if (!_ready) return;
        if (InvokeRequired) { BeginInvoke(() => Emit(ev, data)); return; }
        Post(new JsonObject { ["event"] = ev, ["data"] = data });
    }

    public Task WhenReady => _readyTcs.Task;

    public void ForceClose() { _allowClose = true; Close(); }

    /// <summary>Başka bir kopyadan gelen dosyaları açar ve pencereyi öne getirir.</summary>
    public void OpenFromOtherInstance(List<string> files)
    {
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        if (!Headless) { Show(); Activate(); Native.SetForegroundWindow(Handle); }
        if (files.Count > 0) Emit("open-files", new JsonObject { ["paths"] = new JsonArray(files.Select(f => (JsonNode)f).ToArray()) });
    }

    // ===================== Kapanış =====================
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_allowClose && _ready && e.CloseReason is not (CloseReason.WindowsShutDown or CloseReason.TaskManagerClosing))
        {
            e.Cancel = true;
            Emit("close-request");
            return;
        }
        SavePlacement();
        base.OnFormClosing(e);
    }

    void SavePlacement()
    {
        if (Headless || _fullscreen) return;
        var b = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        Store.SaveWindow(new Store.WindowPlacement(b.X, b.Y, b.Width, b.Height, WindowState == FormWindowState.Maximized));
    }

    void OnPreference(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color)
            BeginInvoke(() => Emit("theme-changed", new JsonObject { ["dark"] = !ThemeInfo.AppsUseLight() }));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SystemEvents.UserPreferenceChanged -= OnPreference;
            _watcher?.Dispose();
            foreach (var w in _fileWatchers.Values) w.Dispose();
            _print?.Dispose();
            _updates?.Dispose();
        }
        base.Dispose(disposing);
    }
}
