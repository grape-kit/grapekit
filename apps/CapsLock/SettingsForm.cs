using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace CapsGosterge;

internal interface ITrayHost
{
    AppSettings Settings { get; }
    Stats Stats { get; }
    bool[] LockStates { get; }
    bool IsPaused { get; }
    DateTime PausedUntil { get; }
    bool HotkeyOk { get; }
    bool? AutoStartState { get; }
    string UpdateStatus { get; }
    Updater.Info? AvailableUpdate { get; }
    bool UpdateBusy { get; }

    void SettingsChanged();
    void PreviewOsd(LockKey key, bool on);
    void PreviewSound(bool on);
    void PreviewSpeech(bool on);
    void PreviewWarning();
    Bitmap RenderPreview(bool on);
    void Pause(TimeSpan? duration);
    void Resume();
    void SuspendHotkey(bool suspend);
    void CheckUpdates();
    void InstallUpdate();
    void ResetSettings();
}

internal sealed class SettingsForm : Form
{
    public const int PageCount = 10;
    static readonly string[] NavIcons =
        ["", "", "", "", "", "", "", "", "", ""];
    static readonly string[] NavKeys =
        ["nav_general", "nav_look", "nav_osd", "nav_sound", "nav_protect", "nav_password", "nav_hotkey", "nav_quiet", "nav_stats", "nav_about"];

    readonly ITrayHost _host;
    AppSettings S_ => _host.Settings;
    Palette _p;
    readonly Fonts _f = new();
    readonly Panel _nav;
    Page _page;
    readonly List<NavButton> _navButtons = [];
    int _current;
    Icon? _icon;
    Action? _refresh;          // sayfadaki "etkin/pasif" durumlarını günceller
    bool _previewOn = true;

    static int _lastPage;

    public int CurrentPage => _current;

    public SettingsForm(ITrayHost host, Palette p, int page = -1)
    {
        _host = host; _p = p;
        _current = page >= 0 ? page : _lastPage;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.None;
        DoubleBuffered = true;
        KeyPreview = true;

        _nav = new DoubleBufferedPanel();
        _nav.Paint += PaintNav;
        _page = new Page(_p, _f);
        Controls.Add(_page);
        Controls.Add(_nav);
        BuildAll();
    }

    sealed class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }
    }

    int S(int v) => LogicalToDeviceUnits(v);

    // ── Dışarıdan çağrılanlar

    public void ApplyPalette(Palette p)
    {
        if (p == _p) return;
        _p = p;
        Rebuild();
    }

    public void Rebuild()
    {
        if (IsHandleCreated) BeginInvoke(BuildAll); else BuildAll();
    }

    public void RefreshPage(params int[] pages)
    {
        if (pages.Length == 0 || pages.Contains(_current))
        {
            if (IsHandleCreated) BeginInvoke(() => ShowPage(_current, keepScroll: true));
        }
    }

    public void OnLockStates()
    {
        _nav.Invalidate();
        _page.Invalidate();
    }

    public void Navigate(int page) => ShowPage(page);

    // ── Kurulum

    void BuildAll()
    {
        if (IsDisposed) return;
        SuspendLayout();
        _f.Create(DeviceDpi);
        Text = L.T("app");
        var oldIcon = _icon;
        // Grapekit standardı: pencere simgesi uygulamanın kendi simgesidir (app.ico)
        try { _icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? Render.AppIcon(_p); } catch { _icon = Render.AppIcon(_p); }
        Icon = _icon;
        oldIcon?.Dispose();
        BackColor = _p.Bg;
        ApplyTitleBar();

        int navW = S(244), pageW = S(640), h = S(700);
        var wa = Screen.FromControl(this).WorkingArea;
        h = Math.Min(h, wa.Height - S(60));
        ClientSize = new Size(navW + pageW, h);

        _nav.Bounds = new Rectangle(0, 0, navW, h);
        _nav.BackColor = _p.HighContrast ? _p.Bg : _p.Dark ? ThemeInfo.C(0x1C1C1C) : ThemeInfo.C(0xEEEEEE);
        foreach (var b in _navButtons) b.Dispose();
        _navButtons.Clear();
        int y = S(104);
        for (int i = 0; i < PageCount; i++)
        {
            int idx = i;
            var b = new NavButton(_p, NavIcons[i], L.T(NavKeys[i]), _f.Nav, _nav.BackColor)
            {
                Bounds = new Rectangle(S(10), y, navW - S(20), S(40)),
                Selected = i == _current,
                TabIndex = i,
            };
            b.Click += (_, _) => ShowPage(idx);
            _nav.Controls.Add(b);
            _navButtons.Add(b);
            y += S(44);
        }

        // Sayfa yeniden oluşturulur (tema değişince renkler değişir)
        var old = _page;
        _page = new Page(_p, _f) { Bounds = new Rectangle(navW, 0, pageW, h), TabIndex = 100 };
        Controls.Add(_page);
        old.Dispose();

        ShowPage(_current);
        ResumeLayout();
        _nav.Invalidate();
    }

    void PaintNav(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        bool caps = _host.LockStates[0];
        var key = new RectangleF(S(20), S(24), S(48), S(48));
        Render.Keycap(g, key, caps, _p);
        TextRenderer.DrawText(g, L.T("app_short"), _f.Heading, new Point(S(80), S(26)), _p.Text, TextFormatFlags.NoPrefix);
        string status = _host.IsPaused ? L.T("paused_short") : L.T(caps ? "caps_on_short" : "caps_off_short");
        Color sc = _host.IsPaused ? _p.Warning : caps ? _p.AccentText : _p.SubText;
        TextRenderer.DrawText(g, status, _f.Desc, new Point(S(80), S(50)), sc, TextFormatFlags.NoPrefix);

        TextRenderer.DrawText(g, "v" + AppInfo.VersionText + (AppInfo.IsPackaged ? " · Store" : ""), _f.Small,
            new Rectangle(S(20), _nav.Height - S(30), _nav.Width - S(40), S(20)), _p.SubText, TextFormatFlags.NoPrefix);
    }

    void ApplyTitleBar()
    {
        if (!IsHandleCreated) return;
        int dark = _p.Dark ? 1 : 0;
        if (Native.DwmSetWindowAttribute(Handle, Native.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, 4) != 0)
            Native.DwmSetWindowAttribute(Handle, Native.DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref dark, 4);
        int color = _p.Bg.R | (_p.Bg.G << 8) | (_p.Bg.B << 16);
        Native.DwmSetWindowAttribute(Handle, Native.DWMWA_CAPTION_COLOR, ref color, 4); // yalnızca Windows 11
    }

    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); ApplyTitleBar(); }
    protected override void OnDpiChanged(DpiChangedEventArgs e) { base.OnDpiChanged(e); Rebuild(); }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape) Close();
        else if (e.Control && e.KeyCode == Keys.Tab) { ShowPage((_current + (e.Shift ? PageCount - 1 : 1)) % PageCount); e.Handled = true; }
        base.OnKeyDown(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _lastPage = _current;
        base.OnFormClosed(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _f.Dispose(); _icon?.Dispose(); }
        base.Dispose(disposing);
    }

    // ── Sayfa oluşturma yardımcıları

    int _x, _y, _cw, _cardTop;

    void ShowPage(int index, bool keepScroll = false)
    {
        if (IsDisposed) return;
        var scroll = keepScroll ? -_page.AutoScrollPosition.Y : 0;
        _current = index;
        for (int i = 0; i < _navButtons.Count; i++) _navButtons[i].Selected = i == index;

        _page.SuspendLayout();
        _page.AutoScrollPosition = Point.Empty;
        _page.Clear();
        _refresh = null;
        _x = S(28);
        _y = S(24);
        _cw = _page.Width - S(28) - S(28) - SystemInformation.VerticalScrollBarWidth / 2;

        switch (index)
        {
            case 0: PageGeneral(); break;
            case 1: PageLook(); break;
            case 2: PageOsd(); break;
            case 3: PageSound(); break;
            case 4: PageProtect(); break;
            case 5: PagePassword(); break;
            case 6: PageHotkey(); break;
            case 7: PageQuiet(); break;
            case 8: PageStats(); break;
            default: PageAbout(); break;
        }

        _y += S(24);
        _page.AutoScrollMinSize = new Size(0, _y);
        _refresh?.Invoke();
        _page.ResumeLayout();
        if (scroll > 0) _page.AutoScrollPosition = new Point(0, scroll);
        _page.Invalidate();
        _nav.Invalidate();
    }

    void Title(string key)
    {
        _page.Headings.Add((L.T(key), new Point(_x, _y), true));
        _y += S(52);
    }

    void Heading(string key)
    {
        _y += S(6);
        _page.Headings.Add((L.T(key), new Point(_x + S(2), _y), false));
        _y += S(28);
    }

    void StartCard() { _cardTop = _y; _y += S(4); }
    void EndCard() { _y += S(4); _page.Cards.Add(new Rectangle(_x, _cardTop, _cw, _y - _cardTop)); _y += S(14); }
    void Divider() => _page.Dividers.Add((_x + S(16), _x + _cw - S(16), _y));

    T Add<T>(T c, Rectangle bounds, string name) where T : Control
    {
        c.Bounds = bounds;
        c.AccessibleName ??= name;
        _page.Controls.Add(c);
        return c;
    }

    ToggleRow Toggle(string key, bool value, Action<bool> set, string? descOverride = null)
    {
        var t = new ToggleRow(_p, L.T(key), descOverride ?? L.T(key + "_desc"), value, _f.Row, _f.Desc);
        t.CheckedChanged += v => { set(v); Changed(); _refresh?.Invoke(); };
        Add(t, new Rectangle(_x + S(4), _y, _cw - S(8), S(60)), L.T(key));
        _y += S(60);
        return t;
    }

    T Row<T>(string title, string? desc, T ctl, int ctlW, int ctlH, int rowH = 60) where T : Control
    {
        int w = S(ctlW), h = S(ctlH), rh = S(rowH);
        _page.Labels.Add((title, desc, new Rectangle(_x + S(16), _y, _cw - w - S(48), rh)));
        Add(ctl, new Rectangle(_x + _cw - S(16) - w, _y + (rh - h) / 2, w, h), title);
        _y += rh;
        return ctl;
    }

    void Para(string text, Color? color = null, bool inCard = false)
    {
        int pad = inCard ? S(16) : S(2);
        int w = _cw - 2 * pad;
        var sz = TextRenderer.MeasureText(text, _f.Desc, new Size(w, int.MaxValue), TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
        int top = _y + (inCard ? S(10) : 0);
        _page.Paragraphs.Add((text, new Rectangle(_x + pad, top, w, sz.Height + S(4)), color ?? _p.SubText));
        _y = top + sz.Height + (inCard ? S(14) : S(12));
    }

    FlatButton Button(string text, bool primary, Rectangle r, Action click, string? icon = null)
    {
        var b = Add(new FlatButton(_p, text, primary, _f.Ctl) { Icon = icon }, r, text);
        b.Click += (_, _) => click();
        return b;
    }

    /// <summary>Sağa hizalı düğme satırı.</summary>
    List<FlatButton> Buttons(string? label, params (string Text, bool Primary, Action Click, string? Icon)[] items)
    {
        int rh = S(56), bh = S(34), gap = S(8);
        if (label != null) _page.Labels.Add((label, null, new Rectangle(_x + S(16), _y, S(160), rh)));
        var list = new List<FlatButton>();
        int right = _x + _cw - S(16);
        foreach (var it in items.Reverse())
        {
            int bw = Math.Max(S(96), TextRenderer.MeasureText(it.Text, _f.Ctl).Width + S(it.Icon != null ? 56 : 32));
            right -= bw;
            list.Insert(0, Button(it.Text, it.Primary, new Rectangle(right, _y + (rh - bh) / 2, bw, bh), it.Click, it.Icon));
            right -= gap;
        }
        _y += rh;
        return list;
    }

    void Changed() => _host.SettingsChanged();

    void AddPainter(Action<Graphics, Point> painter) => _page.Painters.Add(painter);

    // ── 0. Genel

    void PageGeneral()
    {
        Title("nav_general");

        // Canlı durum: üç tuş
        StartCard();
        int top = _y, tileH = S(108), tileW = (_cw - S(16) * 2 - S(12) * 2) / 3;
        string[] names = ["Caps Lock", "Num Lock", "Scroll Lock"];
        string[] glyphs = ["A", "1", "↕"];
        int x0 = _x + S(16);
        AddPainter((g, o) =>
        {
            var states = _host.LockStates;
            for (int i = 0; i < 3; i++)
            {
                var r = new Rectangle(x0 + i * (tileW + S(12)) + o.X, top + S(10) + o.Y, tileW, tileH - S(20));
                Draw.Fill(g, r, S(8), _p.Dark ? ThemeInfo.C(0x323232) : ThemeInfo.C(0xF7F7F7));
                var key = new RectangleF(r.X + S(14), r.Y + (r.Height - S(48)) / 2f, S(48), S(48));
                string glyph = i == 0 ? (states[0] ? "A" : "a") : glyphs[i];
                Render.Keycap(g, key, glyph, states[i] ? 1 : 0, _p);
                int tx = (int)key.Right + S(12);
                TextRenderer.DrawText(g, names[i], _f.Row, new Rectangle(tx, r.Y + S(20), r.Right - tx - S(4), _f.Row.Height), _p.Text, TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                TextRenderer.DrawText(g, L.T(states[i] ? "state_on" : "state_off"), _f.Heading,
                    new Rectangle(tx, r.Y + S(22) + _f.Row.Height, r.Right - tx - S(4), _f.Heading.Height),
                    states[i] ? _p.AccentText : _p.SubText, TextFormatFlags.NoPrefix);
            }
        });
        _y += tileH;
        EndCard();

        Heading("watch_head");
        StartCard();
        Toggle("watch_caps", S_.WatchCaps, v => S_.WatchCaps = v);
        Divider();
        Toggle("watch_num", S_.WatchNum, v => S_.WatchNum = v);
        Divider();
        Toggle("watch_scroll", S_.WatchScroll, v => S_.WatchScroll = v);
        EndCard();

        Heading("pause_head");
        StartCard();
        string status = _host.IsPaused
            ? (_host.PausedUntil == DateTime.MaxValue ? L.T("paused_forever") : string.Format(L.T("paused_until"), _host.PausedUntil.ToString("t", L.Culture)))
            : L.T("not_paused");
        if (_host.IsPaused)
        {
            _page.Labels.Add((status, L.T("pause_desc"), new Rectangle(_x + S(16), _y, _cw - S(200), S(64))));
            Button(L.T("resume"), true, new Rectangle(_x + _cw - S(16) - S(140), _y + S(15), S(140), S(34)), () => { _host.Resume(); }, "");
            _y += S(64);
        }
        else
        {
            _page.Labels.Add((status, L.T("pause_desc"), new Rectangle(_x + S(16), _y, _cw - S(32), S(56))));
            _y += S(52);
            int bw = (_cw - S(32) - S(16)) / 3, bx = _x + S(16);
            Button(L.T("pause_15"), false, new Rectangle(bx, _y, bw, S(34)), () => _host.Pause(TimeSpan.FromMinutes(15)));
            Button(L.T("pause_60"), false, new Rectangle(bx + bw + S(8), _y, bw, S(34)), () => _host.Pause(TimeSpan.FromHours(1)));
            Button(L.T("pause_inf"), false, new Rectangle(bx + 2 * (bw + S(8)), _y, bw, S(34)), () => _host.Pause(null));
            _y += S(34) + S(14);
        }
        EndCard();

        Heading("start_head");
        StartCard();
        string? autoDesc = _host.AutoStartState == null ? L.T("auto_blocked") : null;
        Toggle("auto", S_.AutoStart, v => S_.AutoStart = v, autoDesc);
        Divider();
        Toggle("eco", S_.EfficiencyMode, v => S_.EfficiencyMode = v);
        EndCard();
    }

    // ── 1. Görünüm

    void PageLook()
    {
        Title("nav_look");
        StartCard();
        var theme = Row(L.T("theme"), L.T("theme_desc"), new Segmented(_p, L.List("theme_opts"), S_.Theme, _f.Ctl), 270, 34);
        theme.SelectedChanged += i => { S_.Theme = i; Changed(); };
        Divider();
        var lang = Row(L.T("lang"), L.T("lang_desc"), new Segmented(_p, L.List("lang_opts"), S_.Language, _f.Ctl), 270, 34);
        lang.SelectedChanged += i => { S_.Language = i; L.Lang = i; Changed(); Rebuild(); };
        EndCard();

        Heading("card_head");
        StartCard();
        var preview = Add(new CardPreview(_p), new Rectangle(_x + S(16), _y + S(14), _cw - S(32), S(150)), "preview");
        void UpdatePreview() => preview.SetPreview(_host.RenderPreview(_previewOn), S_.Opacity);
        UpdatePreview();
        _y += S(150) + S(14);
        var which = Add(new Segmented(_p, [L.T("prev_on"), L.T("prev_off")], _previewOn ? 0 : 1, _f.Ctl),
            new Rectangle(_x + S(16), _y, S(260), S(32)), "which");
        which.SelectedChanged += i => { _previewOn = i == 0; UpdatePreview(); };
        Button(L.T("try_now"), false, new Rectangle(_x + _cw - S(16) - S(150), _y, S(150), S(32)), () => _host.PreviewOsd(LockKey.Caps, _previewOn), "");
        _y += S(32) + S(14);
        Divider();

        _page.Labels.Add((L.T("color"), L.T("color_desc"), new Rectangle(_x + S(16), _y, _cw - S(32), S(52))));
        _y += S(50);
        var sw = Add(new ColorSwatches(_p, S_.AccentArgb), new Rectangle(_x + S(12), _y, _cw - S(24), S(44)), L.T("color"));
        sw.ColorChanged += c => { S_.AccentArgb = c; Changed(); };
        _y += S(44) + S(10);
        Divider();

        var size = Row(L.T("size"), null, new Segmented(_p, L.List("size_opts"), S_.CardSize, _f.Ctl), 340, 34, 56);
        size.SelectedChanged += i => { S_.CardSize = i; Changed(); UpdatePreview(); };
        Divider();
        var op = Row(L.T("opacity"), L.T("opacity_desc"), new Slider(_p, S_.Opacity, 30, 100, 5, v => v + "%", _f.Ctl), 280, 36);
        op.ValueChanged += v => { S_.Opacity = v; UpdatePreview(); };
        op.Committed += _ => Changed();
        EndCard();

        StartCard();
        Toggle("badge_t", S_.ShowBadge, v => S_.ShowBadge = v);
        EndCard();
    }

    // ── 2. Ekran bildirimi

    void PageOsd()
    {
        Title("nav_osd");
        StartCard();
        Toggle("osd_show", S_.ShowOsd, v => S_.ShowOsd = v);
        Divider();

        int rowH = S(170);
        _page.Labels.Add((L.T("pos"), L.T("pos_desc"), new Rectangle(_x + S(16), _y - S(16), _cw - S(260), rowH)));
        var picker = Add(new PositionPicker(_p, S_.Position), new Rectangle(_x + _cw - S(16) - S(210), _y + S(12), S(210), S(148)), L.T("pos"));
        int pillY = _y + rowH / 2 + S(26);
        AddPainter((g, o) =>
        {
            string txt = string.Format(L.T("selected"), L.List("pos_names")[S_.Position]);
            var sz = TextRenderer.MeasureText(txt, _f.Ctl, Size.Empty, TextFormatFlags.NoPrefix);
            var pill = new RectangleF(_x + S(14) + o.X, pillY + o.Y, sz.Width + S(10), _f.Ctl.Height + S(8));
            Draw.Fill(g, pill, pill.Height / 2, ThemeInfo.Mix(_p.Accent, _p.Card, 0.82));
            TextRenderer.DrawText(g, txt, _f.Ctl, Rectangle.Round(pill), _p.AccentText,
                TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        });
        picker.SelectedChanged += i => { S_.Position = i; Changed(); _page.Invalidate(); _host.PreviewOsd(LockKey.Caps, _host.LockStates[0]); };
        _y += rowH;
        Divider();

        var dur = Row(L.T("dur"), L.T("dur_desc"),
            new Stepper(_p, S_.DurationMs, AppSettings.MinDuration, AppSettings.MaxDuration, AppSettings.DurationStep, L.Seconds, _f.Ctl), 160, 34);
        dur.ValueChanged += v => { S_.DurationMs = v; Changed(); _host.PreviewOsd(LockKey.Caps, _host.LockStates[0]); };
        Divider();
        Buttons(L.T("try"),
            (L.T("try_on"), false, () => _host.PreviewOsd(LockKey.Caps, true), null),
            (L.T("try_off"), false, () => _host.PreviewOsd(LockKey.Caps, false), null),
            ("Num Lock", false, () => _host.PreviewOsd(LockKey.Num, true), null));
        EndCard();

        _refresh = () =>
        {
            picker.Enabled = S_.ShowOsd || S_.ShowBadge;
            dur.Enabled = S_.ShowOsd;
        };
    }

    // ── 3. Ses

    void PageSound()
    {
        Title("nav_sound");
        StartCard();
        Toggle("sound", S_.Sound, v => { S_.Sound = v; if (v) _host.PreviewSound(true); });
        Divider();
        var pack = Row(L.T("pack"), L.T("pack_desc"), new DropDown(_p, L.List("pack_opts"), S_.SoundPack, _f.Ctl), 220, 34);
        pack.SelectedChanged += i => { S_.SoundPack = i; Changed(); _host.PreviewSound(true); ShowPage(_current, true); };
        Divider();
        var vol = Row(L.T("vol"), null, new Slider(_p, S_.VolumePercent, 0, 100, 5, v => v + "%", _f.Ctl), 280, 36, 56);
        vol.ValueChanged += v => S_.VolumePercent = v;
        vol.Committed += _ => { Changed(); _host.PreviewSound(true); };

        var custom = new List<Control>();
        if (S_.SoundPack == SoundBank.CustomPack)
        {
            Divider();
            custom.Add(WavRow("custom_on", S_.CustomOnWav, p => S_.CustomOnWav = p));
            Divider();
            custom.Add(WavRow("custom_off", S_.CustomOffWav, p => S_.CustomOffWav = p));
        }
        Divider();
        var listen = Buttons(L.T("listen"),
            (L.T("listen_on"), false, () => _host.PreviewSound(true), null),
            (L.T("listen_off"), false, () => _host.PreviewSound(false), null));
        EndCard();

        Heading("speak_head");
        StartCard();
        Toggle("speak", S_.Speak, v => { S_.Speak = v; if (v) _host.PreviewSpeech(true); });
        Divider();
        var rate = Row(L.T("rate"), null, new Segmented(_p, L.List("rate_opts"), S_.SpeakRate, _f.Ctl), 270, 34, 56);
        rate.SelectedChanged += i => { S_.SpeakRate = i; Changed(); _host.PreviewSpeech(true); };
        Divider();
        string? voice = Speech.VoiceName(S_.Language);
        Para(voice != null ? string.Format(L.T("voice_ok"), voice) : L.T("voice_missing"), voice != null ? _p.SubText : _p.Warning, inCard: true);
        Divider();
        var sp = Buttons(L.T("listen"),
            (L.T("listen_on"), false, () => _host.PreviewSpeech(true), null),
            (L.T("listen_off"), false, () => _host.PreviewSpeech(false), null));
        EndCard();

        _refresh = () =>
        {
            pack.Enabled = vol.Enabled = S_.Sound;
            foreach (var c in custom) c.Enabled = S_.Sound;
            foreach (var c in listen) c.Enabled = S_.Sound;
            rate.Enabled = S_.Speak;
            foreach (var c in sp) c.Enabled = S_.Speak;
        };
    }

    Control WavRow(string key, string path, Action<string> set)
    {
        string desc = string.IsNullOrEmpty(path) ? L.T("no_file") : Path.GetFileName(path);
        var btn = Row(L.T(key), desc, new FlatButton(_p, L.T("choose_file"), false, _f.Ctl) { Icon = "" }, 170, 34);
        btn.Click += (_, _) =>
        {
            using var dlg = new OpenFileDialog { Filter = "WAV (*.wav)|*.wav", Title = L.T(key) };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            set(dlg.FileName);
            Changed();
            _host.PreviewSound(key == "custom_on");
            ShowPage(_current, true);
        };
        return btn;
    }

    // ── 4. Koruma

    void PageProtect()
    {
        Title("nav_protect");
        Para(L.T("protect_intro"));
        StartCard();
        var mode = Row(L.T("protect_mode"), null, new Segmented(_p, L.List("protect_opts"), S_.Protection, _f.Ctl), 330, 34, 56);
        Divider();
        var hold = Row(L.T("hold"), L.T("hold_desc"),
            new Stepper(_p, S_.HoldMs, 200, 2000, 100, L.Seconds, _f.Ctl), 160, 34);
        hold.ValueChanged += v => { S_.HoldMs = v; Changed(); };
        Divider();
        var dbl = Row(L.T("double"), L.T("double_desc"),
            new Stepper(_p, S_.DoubleMs, 200, 1000, 50, L.Seconds, _f.Ctl), 160, 34);
        dbl.ValueChanged += v => { S_.DoubleMs = v; Changed(); };
        Divider();
        var single = Toggle("single_off", S_.SinglePressTurnsOff, v => S_.SinglePressTurnsOff = v);
        Divider();
        var hint = Toggle("prot_hint", S_.ShowProtectionHint, v => S_.ShowProtectionHint = v);
        EndCard();

        int tipTop = _y;
        Para(" \n ");
        AddPainter((g, o) =>
        {
            string tip = L.T(S_.Protection switch { 1 => "protect_tip_hold", 2 => "protect_tip_double", _ => "protect_tip_off" });
            var r = new Rectangle(_x + o.X, tipTop + o.Y, _cw, S(60));
            using var f = new Font(Draw.IconFont, _f.Row.Size, FontStyle.Regular, GraphicsUnit.Pixel);
            TextRenderer.DrawText(g, "", f, new Point(r.X + S(2), r.Y + S(2)), _p.AccentText);
            TextRenderer.DrawText(g, tip, _f.Desc, new Rectangle(r.X + S(28), r.Y, r.Width - S(28), r.Height), _p.SubText,
                TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
        });

        mode.SelectedChanged += i => { S_.Protection = i; Changed(); _refresh?.Invoke(); _page.Invalidate(); };
        _refresh = () =>
        {
            hold.Enabled = S_.Protection == 1;
            dbl.Enabled = S_.Protection == 2;
            single.Enabled = hint.Enabled = S_.Protection != 0;
        };
    }

    // ── 5. Şifre uyarısı

    void PagePassword()
    {
        Title("nav_password");
        Para(L.T("pw_intro"));
        StartCard();
        Toggle("pw", S_.PasswordWarning, v => S_.PasswordWarning = v);
        Divider();
        var snd = Toggle("pw_sound", S_.PasswordWarningSound, v => S_.PasswordWarningSound = v);
        Divider();
        Buttons(L.T("try"), (L.T("pw_preview"), false, () => _host.PreviewWarning(), null));
        EndCard();

        Heading("pw_test_head");
        StartCard();
        Para(L.T("pw_test_desc"), inCard: true);
        var tb = new TextBox
        {
            UseSystemPasswordChar = true,
            Font = _f.Row,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = _p.Dark ? ThemeInfo.C(0x1F1F1F) : Color.White,
            ForeColor = _p.Text,
            PlaceholderText = L.T("pw_placeholder"),
        };
        Add(tb, new Rectangle(_x + S(16), _y, Math.Min(S(320), _cw - S(32)), S(30)), L.T("pw_placeholder"));
        _y += S(30) + S(18);
        EndCard();

        Para(L.T("pw_privacy"));
        _refresh = () => snd.Enabled = S_.PasswordWarning;
    }

    // ── 6. Kısayol

    void PageHotkey()
    {
        Title("nav_hotkey");
        Para(L.T("hotkey_intro"));
        StartCard();
        Toggle("hotkey", S_.HotkeyEnabled, v => S_.HotkeyEnabled = v);
        Divider();
        var box = Row(L.T("hotkey_combo"), L.T("hotkey_combo_desc"), new HotkeyBox(_p, S_.Hotkey, L.T("hotkey_prompt"), _f.Ctl), 230, 36);
        box.CaptureChanged += c => _host.SuspendHotkey(c);
        box.ComboChanged += c => { S_.Hotkey = c; Changed(); ShowPage(_current, true); };
        EndCard();

        if (S_.HotkeyEnabled)
        {
            bool ok = _host.HotkeyOk;
            Para((ok ? "✓ " : "⚠ ") + (ok ? string.Format(L.T("hotkey_ok"), Hotkey.Describe(S_.Hotkey)) : L.T("hotkey_fail")),
                ok ? _p.SubText : _p.Warning);
        }
        _refresh = () => box.Enabled = S_.HotkeyEnabled;
    }

    // ── 7. Sessiz saatler

    void PageQuiet()
    {
        Title("nav_quiet");
        Para(L.T("quiet_intro"));
        StartCard();
        Toggle("quiet", S_.QuietHours, v => S_.QuietHours = v);
        Divider();
        string Fmt(int m) => new DateTime(2000, 1, 1).AddMinutes(m).ToString("HH:mm");
        var start = Row(L.T("quiet_start"), null, new Stepper(_p, S_.QuietStartMin, 0, 1410, 30, Fmt, _f.Ctl, wrap: true), 160, 34, 56);
        start.ValueChanged += v => { S_.QuietStartMin = v; Changed(); _page.Invalidate(); };
        Divider();
        var end = Row(L.T("quiet_end"), null, new Stepper(_p, S_.QuietEndMin, 0, 1410, 30, Fmt, _f.Ctl, wrap: true), 160, 34, 56);
        end.ValueChanged += v => { S_.QuietEndMin = v; Changed(); _page.Invalidate(); };
        Divider();
        var mode = Row(L.T("quiet_mode"), null, new Segmented(_p, L.List("quiet_opts"), S_.QuietMode, _f.Ctl), 300, 34, 56);
        mode.SelectedChanged += i => { S_.QuietMode = i; Changed(); };
        EndCard();

        int statusTop = _y;
        _y += S(30);
        AddPainter((g, o) =>
        {
            bool now = S_.InQuietHours(DateTime.Now);
            string t = !S_.QuietHours ? "" : now ? L.T("quiet_now") : L.T("quiet_not_now");
            TextRenderer.DrawText(g, t, _f.Desc, new Point(_x + S(2) + o.X, statusTop + o.Y), now ? _p.AccentText : _p.SubText, TextFormatFlags.NoPrefix);
        });

        StartCard();
        Toggle("quiet_fs", S_.QuietWhenFullscreen, v => S_.QuietWhenFullscreen = v);
        EndCard();

        _refresh = () => start.Enabled = end.Enabled = mode.Enabled = S_.QuietHours;
    }

    // ── 8. İstatistikler

    void PageStats()
    {
        Title("nav_stats");
        StartCard();
        Toggle("stats", S_.Stats, v => S_.Stats = v);
        EndCard();

        Heading("today");
        var today = _host.Stats.Get(DateTime.Today);
        (int Value, string Key, string Icon)[] tiles =
        [
            (today.CapsOn, "st_caps", ""),
            (today.Accidental, "st_accidental", ""),
            (today.Blocked, "st_blocked", ""),
            (today.PasswordWarnings, "st_password", ""),
        ];
        int tw = (_cw - S(12)) / 2, th = S(92), top = _y;
        AddPainter((g, o) =>
        {
            using var icon = new Font(Draw.IconFont, _f.Heading.Size, FontStyle.Regular, GraphicsUnit.Pixel);
            for (int i = 0; i < tiles.Length; i++)
            {
                var r = new Rectangle(_x + (i % 2) * (tw + S(12)) + o.X, top + (i / 2) * (th + S(12)) + o.Y, tw, th);
                Draw.Fill(g, r, S(8), _p.Card);
                Draw.Stroke(g, new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1), S(8), _p.Border);
                TextRenderer.DrawText(g, tiles[i].Icon, icon, new Point(r.X + S(16), r.Y + S(16)), i == 1 ? _p.Warning : _p.AccentText);
                TextRenderer.DrawText(g, tiles[i].Value.ToString(L.Culture), _f.Big, new Point(r.X + S(14), r.Y + S(30)), _p.Text, TextFormatFlags.NoPrefix);
                TextRenderer.DrawText(g, L.T(tiles[i].Key), _f.Desc, new Rectangle(r.X + S(44), r.Y + S(14), r.Width - S(54), _f.Desc.Height + S(4)), _p.SubText,
                    TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            }
        });
        _y += 2 * th + S(12) + S(14);

        Heading("last14");
        StartCard();
        var labels = new string[14];
        var caps = new int[14];
        var acc = new int[14];
        for (int i = 0; i < 14; i++)
        {
            var d = DateTime.Today.AddDays(i - 13);
            var st = _host.Stats.Get(d);
            labels[i] = d.ToString("dd", L.Culture);
            caps[i] = st.CapsOn;
            acc[i] = st.Accidental;
        }
        Add(new BarChart(_p, _f.Small) { Labels = labels, Primary = caps, Secondary = acc },
            new Rectangle(_x + S(16), _y + S(12), _cw - S(32), S(180)), "chart");
        _y += S(196);
        int legendTop = _y;
        AddPainter((g, o) =>
        {
            int lx = _x + S(16) + o.X, ly = legendTop + o.Y;
            Draw.Fill(g, new RectangleF(lx, ly + S(4), S(12), S(12)), S(3), _p.Accent);
            TextRenderer.DrawText(g, L.T("st_caps"), _f.Desc, new Point(lx + S(18), ly + S(1)), _p.SubText, TextFormatFlags.NoPrefix);
            int lx2 = lx + S(18) + TextRenderer.MeasureText(L.T("st_caps"), _f.Desc).Width + S(20);
            Draw.Fill(g, new RectangleF(lx2, ly + S(4), S(12), S(12)), S(3), _p.Warning);
            TextRenderer.DrawText(g, L.T("st_accidental"), _f.Desc, new Point(lx2 + S(18), ly + S(1)), _p.SubText, TextFormatFlags.NoPrefix);
        });
        _y += S(30);
        EndCard();

        int week = Enumerable.Range(0, 7).Sum(i => _host.Stats.Get(DateTime.Today.AddDays(-i)).CapsOn);
        int weekAcc = Enumerable.Range(0, 7).Sum(i => _host.Stats.Get(DateTime.Today.AddDays(-i)).Accidental);
        Para(string.Format(L.T("week_summary"), week, weekAcc));
        Para(L.T("stats_privacy"));
        Button(L.T("stats_reset"), false, new Rectangle(_x, _y, S(200), S(34)), () =>
        {
            if (MessageBox.Show(this, L.T("stats_reset_q"), L.T("app"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _host.Stats.Reset();
                ShowPage(_current);
            }
        }, "");
        _y += S(34);
    }

    // ── 9. Hakkında ve güncelleme

    void PageAbout()
    {
        Title("nav_about");
        StartCard();
        int top = _y;
        AddPainter((g, o) =>
        {
            var key = new RectangleF(_x + S(18) + o.X, top + S(16) + o.Y, S(64), S(64));
            Render.Keycap(g, key, true, _p);
            int tx = (int)key.Right + S(16);
            TextRenderer.DrawText(g, L.T("app"), _f.Heading, new Point(tx, top + S(20) + o.Y), _p.Text, TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, string.Format(L.T("version"), AppInfo.VersionText) + (AppInfo.IsPackaged ? " · Microsoft Store" : ""),
                _f.Desc, new Point(tx, top + S(44) + o.Y), _p.SubText, TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, CoreText.T("by") + " · " + Brand.Copyright, _f.Desc, new Point(tx, top + S(62) + o.Y), _p.SubText, TextFormatFlags.NoPrefix);
        });
        _y += S(96);
        EndCard();

        // Grapekit standardı: marka kartı ve destek bağlantıları (tüm uygulamalarda aynı)
        StartCard();
        int btop = _y;
        AddPainter((g, o) =>
        {
            var mark = new RectangleF(_x + S(18) + o.X, btop + S(14) + o.Y, S(52), S(52));
            if (_p.Dark) Draw.Fill(g, RectangleF.Inflate(mark, S(4), S(4)), S(14), Color.FromArgb(40, Brand.Grape));
            Brand.PaintMark(g, mark);
            int tx = (int)mark.Right + S(18);
            TextRenderer.DrawText(g, Brand.Name, _f.Heading, new Point(tx, btop + S(16) + o.Y), _p.Text, TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, CoreText.T("tagline"), _f.Desc, new Point(tx, btop + S(40) + o.Y), _p.SubText, TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, CoreText.T("free_note"), _f.Desc, new Point(tx, btop + S(58) + o.Y), _p.SubText, TextFormatFlags.NoPrefix);
        });
        _y += S(86);
        Para(CoreText.T("support_desc"), inCard: true);
        Buttons(null,
            (CoreText.T("support"), true, () => Brand.Open(Brand.Support), ""),
            (CoreText.T("more_apps"), false, () => Brand.Open(Brand.AllAppsUrl), ""));
        Divider();
        Buttons(null,
            (CoreText.T("source"), false, () => Brand.Open(GrapekitApp.SourceUrl), ""),
            (CoreText.T("report"), false, () => Brand.Open(GrapekitApp.IssuesUrl), ""));
        EndCard();

        Heading("update_head");
        StartCard();
        if (AppInfo.IsPackaged)
        {
            Para(L.T("update_store"), inCard: true);
        }
        else if (!Updater.Supported)
        {
            Para(L.T("update_unavailable"), inCard: true);
        }
        else
        {
            Toggle("auto_update", S_.AutoUpdateCheck, v => S_.AutoUpdateCheck = v);
            Divider();
            _page.Labels.Add((L.T("update_status"), _host.UpdateStatus, new Rectangle(_x + S(16), _y, _cw - S(220), S(60))));
            var check = Button(L.T("update_check"), false, new Rectangle(_x + _cw - S(16) - S(170), _y + S(13), S(170), S(34)), _host.CheckUpdates, "");
            check.Enabled = !_host.UpdateBusy;
            _y += S(60);
            if (_host.AvailableUpdate is { } info)
            {
                Divider();
                if (!string.IsNullOrWhiteSpace(info.Notes)) Para(info.Notes, _p.Text, inCard: true);
                var inst = Button(string.Format(L.T("update_install"), info.Version.ToString(3)), true,
                    new Rectangle(_x + S(16), _y, S(260), S(36)), _host.InstallUpdate, "");
                inst.Enabled = !_host.UpdateBusy;
                _y += S(36) + S(14);
            }
        }
        EndCard();

        Heading("other_head");
        StartCard();
        Buttons(null,
            (L.T("privacy"), false, () => Open(AppInfo.PrivacyUrl), ""),
            (L.T("website"), false, () => Open(AppInfo.Website), ""),
            (L.T("open_folder"), false, () => Open(AppSettings.Dir), ""));
        Divider();
        Buttons(L.T("reset_all"), (L.T("reset"), false, () =>
        {
            if (MessageBox.Show(this, L.T("reset_q"), L.T("app"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                _host.ResetSettings();
        }, ""));
        EndCard();
    }

    static void Open(string target)
    {
        try
        {
            if (!target.StartsWith("http")) Directory.CreateDirectory(target);
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch { }
    }
}
