using Microsoft.Win32;

namespace CapsGosterge;

internal sealed class TrayApp : ApplicationContext, ITrayHost
{
    static readonly int[] Vks = [Native.VK_CAPITAL, Native.VK_NUMLOCK, Native.VK_SCROLL];

    AppSettings _s;
    readonly Control _invoker = new();
    readonly NotifyIcon _tray = new();
    readonly ContextMenuStrip _menu = new();
    readonly Osd _osd = new();
    readonly AudioPlayer _audio = new();
    readonly Stats _stats = new();
    readonly KeyboardMonitor _kb;
    readonly Hotkey _hotkey = new();
    readonly RegisteredWaitHandle _waitReg;
    readonly SynchronizationContext _ctx;

    // Durum denetimi: tuş bırakıldıktan kısa süre sonra (yalnızca o anda) çalışan tek seferlik zamanlayıcı
    readonly System.Windows.Forms.Timer _checkTimer = new() { Interval = 25 };
    int _checkPasses;
    readonly System.Windows.Forms.Timer _pauseTimer = new();
    readonly System.Windows.Forms.Timer _updateTimer = new() { Interval = 6 * 60 * 60 * 1000 };

    FocusWatcher? _focus;
    readonly bool[] _state = new bool[3];
    long _capsOnAt;
    Palette _pal;
    string _sig = "";
    IntPtr _hIcon;
    Icon? _icon;
    SettingsForm? _form;
    bool _hotkeyOk = true, _hotkeySuspended;
    bool? _autoStartState = true;
    DateTime _pausedUntil = DateTime.MinValue;
    string _updateStatus = "";
    Updater.Info? _update;
    bool _updateBusy;
    Rectangle? _pwField;

    public TrayApp(bool showSettings, EventWaitHandle showEvent, int startPage = -1)
    {
        bool firstRun = !AppSettings.Exists;
        _s = AppSettings.Load();
        L.Lang = _s.Language;
        _invoker.CreateControl();
        _ctx = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        for (int i = 0; i < 3; i++) _state[i] = Native.IsToggled(Vks[i]);
        _pal = ThemeInfo.Get(_s.Theme, _s.AccentArgb);

        _kb = new KeyboardMonitor(_ctx);
        _kb.LockKeyReleased += _ => ScheduleCheck();
        _kb.Blocked += OnBlocked;

        _checkTimer.Tick += (_, _) =>
        {
            CheckStates();
            if (--_checkPasses <= 0) _checkTimer.Stop();
        };
        _pauseTimer.Tick += (_, _) => { _pauseTimer.Stop(); if (!IsPaused) Resume(); else SchedulePauseEnd(); };
        _updateTimer.Tick += (_, _) => _ = AutoCheckUpdatesAsync();
        _hotkey.Pressed += () => { if (IsPaused) Resume(); else Pause(null); ShowPauseCard(); };
        _stats.Changed += () => _form?.RefreshPage(8);

        _menu.Font = new Font("Segoe UI", 9.5f);
        _menu.Opening += (_, _) => { CheckStates(); SyncMenu(); };
        BuildMenu();
        _tray.ContextMenuStrip = _menu;
        _tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowSettings(); };
        _tray.BalloonTipClicked += (_, _) => ShowSettings(9);

        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;

        ApplyAll(force: true);
        _tray.Visible = true;
        _s.Save();

        _waitReg = ThreadPool.RegisterWaitForSingleObject(showEvent,
            (_, _) => _invoker.BeginInvoke(() => ShowSettings()), null, -1, false);

        if (Updater.Supported)
        {
            // İlk denetim açılıştan 2 dk sonra (açılışı yavaşlatmasın), sonra 6 saatte bir.
            var first = new System.Windows.Forms.Timer { Interval = 120_000 };
            first.Tick += (_, _) => { first.Dispose(); _ = AutoCheckUpdatesAsync(); };
            first.Start();
            _updateTimer.Start();
        }

        if (showSettings || firstRun) ShowSettings(startPage >= 0 ? startPage : firstRun ? 0 : -1);
        Native.TrimWorkingSet();
    }

    // ── ITrayHost

    public AppSettings Settings => _s;
    public Stats Stats => _stats;
    public bool[] LockStates => _state;
    public bool IsPaused => DateTime.Now < _pausedUntil;
    public DateTime PausedUntil => _pausedUntil;
    public bool HotkeyOk => _hotkeyOk;
    public bool? AutoStartState => _autoStartState;
    public string UpdateStatus => _updateStatus.Length > 0 ? _updateStatus : LastCheckText();
    public Updater.Info? AvailableUpdate => _update;
    public bool UpdateBusy => _updateBusy;

    public void SettingsChanged()
    {
        _s.Normalize();
        bool langChanged = L.Lang != _s.Language;
        L.Lang = _s.Language;
        _s.Save();
        if (langChanged) { BuildMenu(); Speech.Clear(); }
        ApplyAll(force: false);
    }

    public void PreviewOsd(LockKey key, bool on) =>
        _osd.ShowCard(CardFor(key, on), _pal, _s.Position, _s.DurationMs, _s.CardSize, _s.Opacity);

    public void PreviewSound(bool on) =>
        _audio.Play(SoundBank.Get(_s.SoundPack, on, LockKey.Caps, _s.VolumePercent, _s.CustomOnWav, _s.CustomOffWav));

    public void PreviewSpeech(bool on) => _ = SpeakAsync(SayKey(LockKey.Caps, on), null);

    public void PreviewWarning()
    {
        _osd.ShowCard(WarningCard(), _pal, _s.Position, Math.Max(_s.DurationMs, 2000), _s.CardSize, _s.Opacity);
        if (_s.PasswordWarningSound) _audio.Play(SoundBank.Warning(_s.VolumePercent));
    }

    public Bitmap RenderPreview(bool on) =>
        Render.Card(CardFor(LockKey.Caps, on), _pal, Osd.SizeFactors[_s.CardSize] * (_form?.DeviceDpi ?? 96) / 96f, out _);

    public void Pause(TimeSpan? duration)
    {
        _pausedUntil = duration is { } d ? DateTime.Now + d : DateTime.MaxValue;
        _osd.HidePopup();
        SchedulePauseEnd();
        OnPauseChanged();
    }

    public void Resume()
    {
        _pausedUntil = DateTime.MinValue;
        _pauseTimer.Stop();
        OnPauseChanged();
    }

    public void SuspendHotkey(bool suspend)
    {
        _hotkeySuspended = suspend;
        _hotkeyOk = _hotkey.Set(_s.HotkeyEnabled && !suspend, _s.Hotkey);
    }

    public void CheckUpdates() => _ = CheckUpdatesAsync(manual: true);

    public void InstallUpdate() => _ = InstallUpdateAsync();

    public void ResetSettings()
    {
        var lang = _s.Language;
        _s = new AppSettings { Language = lang };
        _s.Normalize();
        _s.Save();
        ApplyAll(force: true);
        _form?.Rebuild();
    }

    // ── Uygulama

    void ApplyAll(bool force)
    {
        // Klavye koruması
        _kb.Protection = _s.Protection;
        _kb.HoldMs = _s.HoldMs;
        _kb.DoubleMs = _s.DoubleMs;
        _kb.SinglePressTurnsOff = _s.SinglePressTurnsOff;
        _kb.CapsOn = _state[0];

        // Şifre alanı izleyici: yalnızca açıkken iş parçacığı var
        if (_s.PasswordWarning && _focus == null)
        {
            _focus = new FocusWatcher(_ctx);
            _focus.PasswordFocusChanged += OnPasswordFocus;
            _focus.SetArmed(_state[0]);
        }
        else if (!_s.PasswordWarning && _focus != null)
        {
            _focus.Dispose();
            _focus = null;
            HideWarning();
        }

        if (!_hotkeySuspended) _hotkeyOk = _hotkey.Set(_s.HotkeyEnabled, _s.Hotkey);
        Native.SetEfficiencyMode(_s.EfficiencyMode);
        _ = ApplyAutoStartAsync();

        RefreshTheme(force);
        SyncMenu();
    }

    async Task ApplyAutoStartAsync()
    {
        var st = await AutoStart.ApplyAsync(_s.AutoStart);
        if (st != _autoStartState)
        {
            _autoStartState = st;
            _form?.RefreshPage(0);
        }
    }

    void RefreshTheme(bool force)
    {
        bool taskbarLight = ThemeInfo.TaskbarUsesLight();
        bool taskbarAccent = ThemeInfo.TaskbarUsesAccent();
        var wa = (Screen.PrimaryScreen ?? Screen.AllScreens[0]).WorkingArea;
        string sig = string.Join('|', _s.Theme, _s.AccentArgb, ThemeInfo.AppsUseLight(), taskbarLight, taskbarAccent,
            ThemeInfo.SystemAccent().ToArgb(), SystemInformation.HighContrast, wa, SystemInformation.SmallIconSize,
            L.Lang, _s.Position, _s.Opacity, _s.ShowBadge, _s.WatchCaps);
        if (!force && sig == _sig) return;
        _sig = sig;

        _pal = ThemeInfo.Get(_s.Theme, _s.AccentArgb);
        _menu.Renderer = new MenuRenderer(_pal);
        UpdateTray();
        UpdateBadge();
        _form?.ApplyPalette(_pal);
    }

    void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e) =>
        _invoker.BeginInvoke(() => RefreshTheme(false));

    void OnDisplayChanged(object? sender, EventArgs e) => _invoker.BeginInvoke(() => RefreshTheme(true));

    void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume) _invoker.BeginInvoke(ScheduleCheck);
    }

    void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (e.Reason is SessionSwitchReason.SessionUnlock or SessionSwitchReason.ConsoleConnect or SessionSwitchReason.RemoteConnect)
            _invoker.BeginInvoke(ScheduleCheck);
    }

    // ── Kilit tuşları

    void ScheduleCheck()
    {
        CheckStates();
        _checkPasses = 4; // ~100 ms boyunca birkaç kez bak, sonra dur
        _checkTimer.Stop();
        _checkTimer.Start();
    }

    void CheckStates()
    {
        for (int i = 0; i < 3; i++)
        {
            bool now = Native.IsToggled(Vks[i]);
            if (now == _state[i]) continue;
            _state[i] = now;
            OnLockChanged((LockKey)i, now);
        }
    }

    (bool Visual, bool Audio) Gate()
    {
        if (IsPaused) return (false, false);
        bool quiet = _s.InQuietHours(DateTime.Now) || (_s.QuietWhenFullscreen && Native.IsBusyFullscreen());
        if (!quiet) return (true, true);
        return _s.QuietMode == 0 ? (true, false) : (false, false);
    }

    void OnLockChanged(LockKey key, bool on)
    {
        if (key == LockKey.Caps)
        {
            _kb.CapsOn = on;
            UpdateTray();
            UpdateBadge();
            _focus?.SetArmed(on);
            if (!on) HideWarning();
            if (_s.Stats)
            {
                if (on) { _capsOnAt = Environment.TickCount64; _stats.Add(d => d.CapsOn++); }
                else if (Environment.TickCount64 - _capsOnAt < 3000) _stats.Add(d => d.Accidental++);
            }
        }
        else if (key == LockKey.Num && _s.Stats) _stats.Add(d => d.NumToggles++);

        _form?.OnLockStates();
        if (!_s.Watches(key)) return;

        var (visual, audio) = Gate();
        if (visual && _s.ShowOsd)
            _osd.ShowCard(CardFor(key, on), _pal, _s.Position, _s.DurationMs, _s.CardSize, _s.Opacity);
        if (audio) PlayFor(key, on);
    }

    void PlayFor(LockKey key, bool on)
    {
        var tone = _s.Sound ? SoundBank.Get(_s.SoundPack, on, key, _s.VolumePercent, _s.CustomOnWav, _s.CustomOffWav) : null;
        if (_s.Speak) _ = SpeakAsync(SayKey(key, on), tone);
        else if (tone != null) _audio.Play(tone);
    }

    async Task SpeakAsync(string text, byte[]? before)
    {
        var speech = await Speech.GetAsync(text, _s.Language, _s.SpeakRate, _s.VolumePercent);
        _audio.Play(before, speech);
    }

    static string SayKey(LockKey key, bool on) => L.T(key switch
    {
        LockKey.Caps => on ? "say_caps_on" : "say_caps_off",
        LockKey.Num => on ? "say_num_on" : "say_num_off",
        _ => on ? "say_scroll_on" : "say_scroll_off",
    });

    static CardSpec CardFor(LockKey key, bool on) => key switch
    {
        LockKey.Caps => new CardSpec(on ? "A" : "a", on ? 1 : 0, L.T(on ? "osd_on" : "osd_off"), L.T(on ? "osd_on_sub" : "osd_off_sub")),
        LockKey.Num => new CardSpec("1", on ? 1 : 0, L.T(on ? "num_on" : "num_off"), L.T(on ? "num_on_sub" : "num_off_sub")),
        _ => new CardSpec("↕", on ? 1 : 0, L.T(on ? "scroll_on" : "scroll_off"), L.T(on ? "scroll_on_sub" : "scroll_off_sub")),
    };

    static CardSpec WarningCard() => new("", 2, L.T("pw_warn_title"), L.T("pw_warn_sub"));

    void OnBlocked(int kind)
    {
        if (_s.Stats) _stats.Add(d => d.Blocked++);
        if (!_s.ShowProtectionHint) return;
        var (visual, audio) = Gate();
        if (!visual) return;
        var card = kind == 1
            ? new CardSpec("A", 0, L.T("hint_hold_title"), string.Format(L.T("hint_hold_sub"), L.Seconds(_s.HoldMs)))
            : new CardSpec("A", 0, L.T("hint_double_title"), L.T("hint_double_sub"));
        _osd.ShowCard(card, _pal, _s.Position, 1600, _s.CardSize, _s.Opacity);
    }

    void OnPasswordFocus(Rectangle? field)
    {
        _pwField = field;
        if (field is not { } f || !_state[0] || IsPaused) { HideWarning(); return; }
        _osd.ShowWarning(f, WarningCard(), _pal, _s.CardSize, _s.Opacity);
        if (_s.Stats) _stats.Add(d => d.PasswordWarnings++);
        var (_, audio) = Gate();
        if (audio && _s.PasswordWarningSound)
        {
            var warn = SoundBank.Warning(_s.VolumePercent);
            if (_s.Speak) _ = SpeakAsync(L.T("say_pw"), warn); else _audio.Play(warn);
        }
    }

    void HideWarning()
    {
        _pwField = null;
        _osd.HideWarning();
    }

    void UpdateBadge() => _osd.SetBadge(_s.ShowBadge && _s.WatchCaps && _state[0] && !IsPaused, _pal, _s.Position, _s.Opacity);

    // ── Duraklatma

    void SchedulePauseEnd()
    {
        _pauseTimer.Stop();
        if (_pausedUntil == DateTime.MaxValue || !IsPaused) return;
        var ms = (_pausedUntil - DateTime.Now).TotalMilliseconds;
        _pauseTimer.Interval = (int)Math.Clamp(ms + 500, 1000, int.MaxValue);
        _pauseTimer.Start();
    }

    void OnPauseChanged()
    {
        UpdateTray();
        UpdateBadge();
        SyncMenu();
        _form?.RefreshPage(0);
        _form?.OnLockStates();
    }

    void ShowPauseCard()
    {
        bool p = IsPaused;
        _osd.ShowCard(new CardSpec(p ? "" : "", p ? 2 : 1, L.T(p ? "paused_title" : "resumed_title"),
            p ? string.Format(L.T("paused_sub"), Hotkey.Describe(_s.Hotkey)) : L.T("resumed_sub")),
            _pal, _s.Position, 1500, _s.CardSize, _s.Opacity);
    }

    // ── Tepsi

    void UpdateTray()
    {
        var size = SystemInformation.SmallIconSize.Width;
        IntPtr h = Render.TrayIcon(_state[0], IsPaused, _pal, ThemeInfo.TaskbarUsesLight(), ThemeInfo.TaskbarUsesAccent(), size);
        var icon = Icon.FromHandle(h);
        _tray.Icon = icon;
        _icon?.Dispose();
        if (_hIcon != IntPtr.Zero) Native.DestroyIcon(_hIcon);
        _icon = icon; _hIcon = h;

        string tip = L.T(_state[0] ? "tray_on" : "tray_off") + (IsPaused ? " · " + L.T("paused_short") : "");
        _tray.Text = tip.Length > 63 ? tip[..63] : tip;
    }

    ToolStripMenuItem _miState = null!, _miOsd = null!, _miSound = null!, _miSpeak = null!, _miProtect = null!, _miPause = null!, _miResume = null!;

    void BuildMenu()
    {
        _menu.Items.Clear();
        _miState = new ToolStripMenuItem("") { Font = new Font(_menu.Font, FontStyle.Bold) };
        _miState.Click += (_, _) => ShowSettings(0);

        _miOsd = MenuToggle("menu_osd", () => _s.ShowOsd, v => _s.ShowOsd = v);
        _miSound = MenuToggle("menu_sound", () => _s.Sound, v => _s.Sound = v);
        _miSpeak = MenuToggle("menu_speak", () => _s.Speak, v => _s.Speak = v);
        _miProtect = MenuToggle("menu_protect", () => _s.Protection != 0, v => _s.Protection = v ? Math.Max(1, _lastProtection) : 0);

        _miPause = new ToolStripMenuItem(L.T("menu_pause"));
        foreach (var (key, dur) in new (string, TimeSpan?)[] { ("pause_15", TimeSpan.FromMinutes(15)), ("pause_60", TimeSpan.FromHours(1)), ("pause_inf", null) })
        {
            var mi = new ToolStripMenuItem(L.T(key));
            mi.Click += (_, _) => Pause(dur);
            _miPause.DropDownItems.Add(mi);
        }
        _miResume = new ToolStripMenuItem(L.T("resume"));
        _miResume.Click += (_, _) => Resume();

        var settings = new ToolStripMenuItem(L.T("menu_settings"));
        settings.Click += (_, _) => ShowSettings();
        var exit = new ToolStripMenuItem(L.T("menu_exit"));
        exit.Click += (_, _) => Exit();

        _menu.Items.AddRange([
            _miState, new ToolStripSeparator(),
            _miOsd, _miSound, _miSpeak, _miProtect, new ToolStripSeparator(),
            _miPause, _miResume, new ToolStripSeparator(),
            ..StandardMenu.Items(() => ShowSettings(9)), new ToolStripSeparator(),
            settings, exit,
        ]);
        foreach (ToolStripItem i in _menu.Items) i.Padding = new Padding(0, 3, 0, 3);
        SyncMenu();
    }

    int _lastProtection = 1;

    ToolStripMenuItem MenuToggle(string key, Func<bool> get, Action<bool> set)
    {
        var mi = new ToolStripMenuItem(L.T(key));
        mi.Click += (_, _) =>
        {
            if (_s.Protection != 0) _lastProtection = _s.Protection;
            set(!get());
            SettingsChanged();
            _form?.Rebuild();
        };
        mi.Tag = get;
        return mi;
    }

    void SyncMenu()
    {
        if (_miState == null) return;
        foreach (var mi in new[] { _miOsd, _miSound, _miSpeak, _miProtect })
            if (mi.Tag is Func<bool> get) mi.Checked = get();
        string caps = L.T(_state[0] ? "tray_on" : "tray_off");
        _miState.Text = IsPaused ? caps + "  ·  " + L.T("paused_short") : caps;
        _miState.ForeColor = IsPaused ? _pal.Warning : _state[0] ? _pal.AccentText : _pal.Text;
        _miPause.Visible = !IsPaused;
        _miResume.Visible = IsPaused;
    }

    // ── Güncelleme

    string LastCheckText() =>
        _s.LastUpdateCheck == default ? L.T("upd_never")
            : string.Format(L.T("upd_last"), _s.LastUpdateCheck.ToLocalTime().ToString("g", L.Culture));

    async Task AutoCheckUpdatesAsync()
    {
        if (!_s.AutoUpdateCheck || (DateTime.UtcNow - _s.LastUpdateCheck).TotalHours < 20) return;
        await CheckUpdatesAsync(manual: false);
    }

    async Task CheckUpdatesAsync(bool manual)
    {
        if (_updateBusy || !Updater.Supported) return;
        _updateBusy = true;
        _updateStatus = L.T("upd_checking");
        _form?.RefreshPage(9);
        try
        {
            _update = await Updater.CheckAsync();
            _s.LastUpdateCheck = DateTime.UtcNow;
            _s.Save();
            _updateStatus = _update != null ? string.Format(L.T("upd_available"), _update.Version.ToString(3)) : L.T("upd_latest");
            if (_update != null && !manual && _s.SkippedVersion != _update.Version.ToString())
                _tray.ShowBalloonTip(8000, L.T("balloon_update_title"), string.Format(L.T("balloon_update_text"), _update.Version.ToString(3)), ToolTipIcon.Info);
        }
        catch
        {
            _updateStatus = L.T("upd_error");
        }
        finally
        {
            _updateBusy = false;
            _form?.RefreshPage(9);
        }
    }

    async Task InstallUpdateAsync()
    {
        if (_update == null || _updateBusy) return;
        _updateBusy = true;
        try
        {
            var progress = new Progress<int>(p => { _updateStatus = string.Format(L.T("upd_downloading"), p); _form?.RefreshPage(9); });
            var path = await Updater.DownloadAsync(_update, progress);
            Updater.RunInstaller(path);
            Exit();
        }
        catch (Exception ex)
        {
            _updateStatus = L.T("upd_error") + " " + ex.Message;
            _updateBusy = false;
            _form?.RefreshPage(9);
        }
    }

    // ── Pencere

    void ShowSettings(int page = -1)
    {
        CheckStates();
        if (_form is { IsDisposed: false })
        {
            if (_form.WindowState == FormWindowState.Minimized) _form.WindowState = FormWindowState.Normal;
            if (page >= 0) _form.Navigate(page);
            _form.Show();
            _form.Activate();
            return;
        }
        _form = new SettingsForm(this, _pal, page);
        _form.FormClosed += (_, _) =>
        {
            _form = null;
            _stats.Flush();
            // Pencere kapandı: kullanılmayan belleği hemen geri ver
            _invoker.BeginInvoke(Native.TrimWorkingSet);
        };
        _form.Show();
        _form.Activate();
    }

    void Exit()
    {
        _tray.Visible = false;
        _form?.Close();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            _waitReg.Unregister(null);
            _kb.Dispose();
            _focus?.Dispose();
            _hotkey.Dispose();
            _checkTimer.Dispose();
            _pauseTimer.Dispose();
            _updateTimer.Dispose();
            _tray.Visible = false;
            _tray.Dispose();
            _menu.Dispose();
            _osd.Dispose();
            _audio.Dispose();
            _stats.Dispose();
            _icon?.Dispose();
            if (_hIcon != IntPtr.Zero) Native.DestroyIcon(_hIcon);
            _invoker.Dispose();
        }
        base.Dispose(disposing);
    }
}

