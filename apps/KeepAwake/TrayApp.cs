using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace KeepAwake;

/// <summary>Tepsi simgesi, panel, ayarlar penceresi, kısayol ve güncellemeleri bir arada yürütür.</summary>
internal sealed class TrayApp : ApplicationContext
{
    public Settings Settings { get; }
    public AwakeEngine Engine { get; }
    public UpdateManager Updates { get; }
    public bool HotkeyRegistered { get; private set; }

    readonly NotifyIcon _tray;
    readonly GlobalHotkey _hotkey = new();
    readonly Control _ui = new();
    readonly RegisteredWaitHandle _showWait;
    Kit _kit;
    AwakeFlyout? _flyout;
    SettingsForm? _form;
    Icon? _iconOn, _iconOff;
    bool _balloonIsUpdate;
    EndWarning? _warning;

    public TrayApp(bool showSettings, EventWaitHandle showEvent, int page)
    {
        _ = _ui.Handle; // diğer iş parçacıklarından gelen olayları arayüze aktarmak için
        Settings = Settings.Load();
        L.Lang = Settings.Language;
        _kit = Kit.Get(Settings.Theme);

        Engine = new AwakeEngine(Settings);
        Engine.Changed += SyncUi;
        Engine.Tick += OnTick;
        Engine.Expired += () =>
        {
            if (Settings.EndAction > 0)
            {
                _warning?.Close();
                _warning = new EndWarning(_kit, Settings.EndAction);
                _warning.FormClosed += (_, _) => _warning = null;
                _warning.Show();
                _warning.Activate();
                return;
            }
            if (!Settings.NotifyOnEnd) return;
            _balloonIsUpdate = false;
            _tray!.ShowBalloonTip(5000, L.T("ended_title"), L.T("ended_text"), ToolTipIcon.Info);
        };

        Updates = new UpdateManager(() => Settings.LastUpdateCheck, d => { Settings.LastUpdateCheck = d; Settings.Save(); }, () => Settings.AutoUpdateCheck);
        Updates.Found += info =>
        {
            _balloonIsUpdate = true;
            _tray!.ShowBalloonTip(8000, CoreText.T("balloon_update_title"), string.Format(CoreText.T("balloon_update_text"), GrapekitApp.Name, info.Version.ToString(3)), ToolTipIcon.Info);
        };

        MakeIcons();
        _tray = new NotifyIcon { Icon = _iconOff, Visible = true, Text = GrapekitApp.Name, ContextMenuStrip = new ContextMenuStrip() };
        _tray.ContextMenuStrip.Opening += (_, e) => { BuildMenu(); e.Cancel = false; };
        _tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ToggleFlyout(); };
        _tray.BalloonTipClicked += (_, _) => { if (_balloonIsUpdate) ShowSettings(3); };

        _hotkey.Pressed += () => Engine.Toggle();
        HotkeyRegistered = _hotkey.Set(Settings.HotkeyEnabled, Settings.Hotkey);

        SystemEvents.UserPreferenceChanged += OnPreferenceChanged;
        _showWait = ThreadPool.RegisterWaitForSingleObject(showEvent, (_, _) => _ui.BeginInvoke(() => ShowSettings(-1)), null, -1, false);

        if (!GrapekitApp.IsPackaged) _ = AutoStart.ApplyAsync(Settings.AutoStart);
        if (Settings.StartActive) Engine.Start(Settings.EffectiveSeconds);
        SyncUi();
        if (showSettings) ShowSettings(page);
    }

    // ── Panel ve pencere

    public void ShowPanelOnStart() => _ui.BeginInvoke(ToggleFlyout);

    void ToggleFlyout()
    {
        if (_flyout is { Visible: true }) { _flyout.Hide(); return; }
        // Panel tepsi simgesine tıklandığı için az önce kapandıysa yeniden açma (aç/kapa davranışı)
        if (_flyout != null && (DateTime.UtcNow - _flyout.HiddenAt).TotalMilliseconds < 400) return;
        if (_flyout == null)
        {
            _flyout = new AwakeFlyout(_kit, Engine, Settings, SaveAndSync);
            _flyout.SettingsRequested += () => ShowSettings(0);
        }
        _flyout.Sync();
        _flyout.ShowNearTray();
    }

    public void ShowSettings(int tab)
    {
        if (_form is { IsDisposed: false })
        {
            if (tab >= 0) _form.SelectTab(tab);
            if (_form.WindowState == FormWindowState.Minimized) _form.WindowState = FormWindowState.Normal;
            _form.Activate();
            return;
        }
        _form = new SettingsForm(this, _kit, Math.Max(0, tab));
        _form.FormClosed += (_, _) => { _form = null; };
        _form.Show();
        _form.Activate();
    }

    /// <summary>Ayarı kaydeder ve her yere yansıtır (kısayol, panel, tepsi).</summary>
    public void SaveAndSync()
    {
        Settings.Save();
        HotkeyRegistered = _hotkey.Set(Settings.HotkeyEnabled, Settings.Hotkey);
        SyncUi();
    }

    public void ApplyTheme()
    {
        _kit = Kit.Get(Settings.Theme);
        ResetFlyout();
        _form?.Rebuild(_kit);
    }

    public void ApplyLanguage()
    {
        ResetFlyout();
        _form?.Rebuild();
        SyncUi();
    }

    void ResetFlyout()
    {
        _flyout?.Dispose();
        _flyout = null;
    }

    void OnPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color && Settings.Theme == 0)
            _ui.BeginInvoke(() =>
            {
                var k = Kit.Get(0);
                if (k.Dark == _kit.Dark && !SystemInformation.HighContrast) return;
                ApplyTheme();
            });
    }

    void SyncUi()
    {
        _tray.Icon = Engine.Awake ? _iconOn : _iconOff;
        UpdateTooltip();
        if (_flyout is { Visible: true }) _flyout.Sync();
        if (_form is { IsDisposed: false }) { _form.RefreshHero(); }
    }

    void OnTick()
    {
        UpdateTooltip();
        if (_flyout is { Visible: true }) _flyout.Sync();
        _form?.RefreshHero();
    }

    void UpdateTooltip()
    {
        string state = Engine.Awake
            ? Engine.Reason == Reason.Manual && Engine.Until != null ? string.Format(L.T("hero_awake_time"), L.Span(Engine.Remaining))
            : Engine.Reason == Reason.Manual ? L.T("hero_awake_inf") : L.T("hero_auto")
            : Engine.Paused ? L.T("paused") : "";
        string tip = state.Length == 0 ? string.Format(L.T("tray_off"), GrapekitApp.Name) : string.Format(L.T("tray_on"), GrapekitApp.Name, state);
        if (tip.Length > 127) tip = tip[..127];
        if (_tray.Text != tip) _tray.Text = tip;
    }

    // ── Tepsi menüsü

    void BuildMenu()
    {
        var m = _tray.ContextMenuStrip!;
        foreach (var item in m.Items.Cast<ToolStripItem>().ToArray()) item.Dispose();
        m.Items.Clear();
        m.Renderer = new MenuRenderer(_kit.P);
        m.Font = BrandFonts.Body(14 * m.DeviceDpi / 96f);

        var toggle = new ToolStripMenuItem(Engine.ManualOn ? L.T("stop") : L.T("start")) { Font = BrandFonts.BodySemi(14 * m.DeviceDpi / 96f) };
        toggle.Click += (_, _) => Engine.Toggle();
        m.Items.Add(toggle);

        var dur = new ToolStripMenuItem(L.T("duration"));
        for (int i = 0; i < L.Durations.Length; i++)
        {
            int minutes = L.Durations[i];
            var d = new ToolStripMenuItem(L.DurationLong[i]) { Checked = !Settings.Custom && Settings.Minutes == minutes };
            d.Click += (_, _) => { Engine.SetDuration(minutes); if (!Engine.ManualOn) Engine.Start(minutes * 60); SaveAndSync(); };
            dur.DropDownItems.Add(d);
        }
        var custom = new ToolStripMenuItem($"{L.T("d_custom")} ({L.Span(TimeSpan.FromSeconds(Settings.CustomSeconds))})") { Checked = Settings.Custom };
        custom.Click += (_, _) => { Engine.SetCustom(Settings.CustomSeconds); if (!Engine.ManualOn) Engine.Start(Settings.CustomSeconds); SaveAndSync(); };
        dur.DropDownItems.Add(custom);
        if (dur.DropDown is ToolStripDropDownMenu dd) dd.Renderer = new MenuRenderer(_kit.P);
        m.Items.Add(dur);

        var screen = new ToolStripMenuItem(L.T("keep_screen")) { Checked = Settings.Mode == 1 };
        screen.Click += (_, _) => { Settings.Mode = Settings.Mode == 1 ? 0 : 1; Engine.Apply(); SaveAndSync(); };
        m.Items.Add(screen);

        m.Items.Add(new ToolStripSeparator());
        var settings = new ToolStripMenuItem(CoreText.T("settings"));
        settings.Click += (_, _) => ShowSettings(0);
        m.Items.Add(settings);
        m.Items.AddRange(StandardMenu.Items(() => ShowSettings(3)));
        m.Items.Add(new ToolStripSeparator());
        var quit = new ToolStripMenuItem(CoreText.T("quit"));
        quit.Click += (_, _) => ExitThread();
        m.Items.Add(quit);
    }

    // ── Simgeler: açıkken uygulama simgesi, kapalıyken soluk gri hali

    void MakeIcons()
    {
        int px = SystemInformation.SmallIconSize.Width;
        _iconOn = AppIcon.Get(px) ?? SystemIcons.Application;
        using var src = new Bitmap(px, px);
        using (var g = Graphics.FromImage(src)) g.DrawIcon(_iconOn, new Rectangle(0, 0, px, px));
        using var gray = new Bitmap(px, px);
        using (var g = Graphics.FromImage(gray))
        {
            var cm = new ColorMatrix([
                [0.30f, 0.30f, 0.30f, 0, 0],
                [0.59f, 0.59f, 0.59f, 0, 0],
                [0.11f, 0.11f, 0.11f, 0, 0],
                [0, 0, 0, 0.75f, 0],
                [0, 0, 0, 0, 1],
            ]);
            using var ia = new ImageAttributes();
            ia.SetColorMatrix(cm);
            g.DrawImage(src, new Rectangle(0, 0, px, px), 0, 0, px, px, GraphicsUnit.Pixel, ia);
        }
        var h = gray.GetHicon();
        _iconOff = (Icon)Icon.FromHandle(h).Clone();
        DestroyIcon(h);
    }

    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SystemEvents.UserPreferenceChanged -= OnPreferenceChanged;
            _showWait.Unregister(null);
            _tray.Visible = false;
            _tray.Dispose();
            _hotkey.Dispose();
            Engine.Dispose();
            Updates.Dispose();
            _flyout?.Dispose();
            _warning?.Dispose();
            _form?.Dispose();
            _iconOff?.Dispose();
            _ui.Dispose();
        }
        base.Dispose(disposing);
    }
}
