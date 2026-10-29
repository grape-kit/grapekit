using System.Diagnostics;

namespace KeepAwake;

/// <summary>Kurallar sekmesindeki program etiketleri: her birinde kaldır düğmesi, sonda "+ Program ekle".</summary>
internal sealed class ProcessTags : ThemedControl
{
    readonly List<string> _items;
    readonly string _add;
    int _hover = -2; // -1: ekle düğmesi
    public event Action<string>? Removed;
    public event Action? AddRequested;

    public ProcessTags(Palette p, List<string> items, string add, Font font, Color surface) : base(p, surface)
    {
        _items = items; _add = add; Font = font;
        AccessibleRole = AccessibleRole.List;
        AccessibleName = add;
    }

    List<(int Index, Rectangle Rect, Rectangle X)> Arrange(out int height)
    {
        var list = new List<(int, Rectangle, Rectangle)>();
        int x = 0, y = 0, h = S(36), gap = S(8);
        for (int i = 0; i < _items.Count; i++)
        {
            string t = _items[i] + ".exe";
            int w = TextRenderer.MeasureText(t, Font).Width + S(12 + 8 + 26 + 4);
            if (x > 0 && x + w > Width) { x = 0; y += h + gap; }
            var r = new Rectangle(x, y, w, h);
            list.Add((i, r, new Rectangle(r.Right - S(30), r.Y + S(5), S(26), S(26))));
            x += w + gap;
        }
        int aw = TextRenderer.MeasureText(_add, Font).Width + S(28);
        if (x > 0 && x + aw > Width) { x = 0; y += h + gap; }
        list.Add((-1, new Rectangle(x, y, aw, h), Rectangle.Empty));
        height = y + h;
        return list;
    }

    public int Measure(int width)
    {
        var old = Width;
        Width = width;
        Arrange(out int h);
        Width = old;
        return h;
    }

    int Hit(Point p)
    {
        foreach (var (i, r, xr) in Arrange(out _))
        {
            if (i == -1 && r.Contains(p)) return -1;
            if (i >= 0 && xr.Contains(p)) return i;
        }
        return -2;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int h = Hit(e.Location);
        Cursor = h != -2 ? Cursors.Hand : Cursors.Default;
        if (h != _hover) { _hover = h; Invalidate(); }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e) { _hover = -2; base.OnMouseLeave(e); }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        int h = Hit(e.Location);
        if (h == -1) AddRequested?.Invoke();
        else if (h >= 0) Removed?.Invoke(_items[h]);
        base.OnMouseClick(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Enter or Keys.Space) { AddRequested?.Invoke(); e.Handled = true; }
        else if (e.KeyCode is Keys.Delete or Keys.Back && _items.Count > 0) { Removed?.Invoke(_items[^1]); e.Handled = true; }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = Begin(e);
        foreach (var (i, r, xr) in Arrange(out _))
        {
            if (i == -1)
            {
                Color c = _hover == -1 ? ThemeInfo.Mix(Surface, P.Accent, 0.10) : Surface;
                Draw.Fill(g, r, Sf(10), c);
                using var pen = new Pen(P.AccentText, Sf(1)) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
                using var path = Draw.Round(new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1), Sf(10));
                g.DrawPath(pen, path);
                Center(g, _add, Font, r, P.AccentText);
                if (Focused && ShowFocusCues) Draw.Stroke(g, RectangleF.Inflate(r, -Sf(3), -Sf(3)), Sf(8), P.AccentText);
                continue;
            }
            Draw.Fill(g, r, Sf(10), P.Bg);
            Draw.Stroke(g, new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1), Sf(10), P.Border);
            DrawText(g, _items[i] + ".exe", Font, new Rectangle(r.X + S(12), r.Y, r.Width - S(42), r.Height), P.Text, TextFormatFlags.VerticalCenter);
            if (_hover == i) Draw.Fill(g, xr, Sf(8), P.Neutral);
            using var f = new Font(Draw.IconFont, Sf(9), FontStyle.Regular, GraphicsUnit.Pixel);
            Center(g, "", f, xr, P.SubText);
        }
    }
}

internal sealed class SettingsForm : SettingsWindow
{
    readonly TrayApp _app;
    Settings Cfg => _app.Settings;
    bool? _autostartState = true;

    public SettingsForm(TrayApp app, Kit k, int tab) : base(k, tab, app.Updates) { _app = app; }

    protected override string[] TabNames => [L.T("tab_general"), L.T("tab_rules"), L.T("tab_hotkey"), CoreText.T("about")];

    protected override (string Title, string Sub, string? Action) HeroState()
    {
        var e = _app.Engine;
        string sub = Cfg.Mode == 1 ? L.T("sub_screen") : L.T("sub_system");
        if (e.Paused) return (L.T("hero_paused"), L.T(e.PausedForLowBattery ? "paused_low" : "paused_ac"), e.ManualOn ? L.T("stop") : L.T("start"));
        return e.Reason switch
        {
            Reason.Manual when e.Until != null => (string.Format(L.T("hero_awake_time"), L.Span(e.Remaining)), sub, L.T("stop")),
            Reason.Manual => (L.T("hero_awake_inf"), sub, L.T("stop")),
            Reason.Process => (L.T("hero_auto"), string.Format(L.T("because_proc"), e.ProcessName), L.T("start")),
            Reason.Fullscreen => (L.T("hero_auto"), L.T("because_fs"), L.T("start")),
            _ => (L.T("hero_off"), L.T("sleep_normal"), L.T("start")),
        };
    }

    protected override void OnHeroAction() => _app.Engine.Toggle();

    void Save() => _app.SaveAndSync();

    protected override void BuildTab(int tab, PageBuilder b)
    {
        switch (tab)
        {
            case 0: General(b); break;
            case 1: Rules(b); break;
            case 2: Shortcut(b); break;
            default: b.About(Updates, v => { Cfg.AutoUpdateCheck = v; Save(); }, Cfg.AutoUpdateCheck); break;
        }
    }

    void General(PageBuilder b)
    {
        b.Section(L.T("how"));
        b.Cards([("", L.T("mode_system"), L.T("mode_system_desc")), ("", L.T("mode_screen"), L.T("mode_screen_desc"))],
            Cfg.Mode, i => { Cfg.Mode = i; _app.Engine.Apply(); Save(); RefreshHero(); });

        b.Section(L.T("default_duration"), L.T("default_duration_desc"));
        b.Chips(L.DurationLong, L.ChipIndex(Cfg), i =>
        {
            if (i == L.Durations.Length) _app.Engine.SetCustom(Cfg.CustomSeconds);
            else _app.Engine.SetDuration(L.Durations[i]);
            Save();
            RefreshPage();
        });
        if (Cfg.Custom)
        {
            b.Gap(10);
            b.Note(L.T("custom_len_desc"));
            var box = new DurationBox(K.P, Cfg.CustomSeconds, [L.T("unit_h"), L.T("unit_m"), L.T("unit_s")],
                BrandFonts.Display(18 * DeviceDpi / 96f), b.Fonts.Ctl, b.Surface);
            box.ValueChanged += v => { _app.Engine.SetCustom(v); Save(); };
            b.Add(box, 48, Math.Min(b.Width, (int)(340 * DeviceDpi / 96f)));
        }

        b.Section(L.T("end_action"), L.T("end_action_desc"));
        b.Chips(L.EndActions, Cfg.EndAction, i => { Cfg.EndAction = i; Save(); });

        b.Section(L.T("startup"));
        b.Toggle(L.T("autostart"), _autostartState == null ? L.T("autostart_blocked") : L.T("autostart_desc"), Cfg.AutoStart, async v =>
        {
            Cfg.AutoStart = v; Save();
            var r = await AutoStart.ApplyAsync(v);
            if (r != _autostartState) { _autostartState = r; if (!IsDisposed) RefreshPage(); }
        });
        b.Toggle(L.T("start_active"), L.T("start_active_desc"), Cfg.StartActive, v => { Cfg.StartActive = v; Save(); });
        b.Toggle(L.T("notify_end"), L.T("notify_end_desc"), Cfg.NotifyOnEnd, v => { Cfg.NotifyOnEnd = v; Save(); });

        b.Section(L.T("appearance"));
        b.Chips([L.T("theme_system"), L.T("theme_light"), L.T("theme_dark")], Cfg.Theme, i => { Cfg.Theme = i; Save(); BeginInvoke(_app.ApplyTheme); });

        b.Section(L.T("language"));
        b.Chips(["Türkçe", "English"], Cfg.Language, i => { Cfg.Language = i; L.Lang = i; Save(); BeginInvoke(_app.ApplyLanguage); });
    }

    void Rules(PageBuilder b)
    {
        b.BeginSoft();
        b.Section(L.T("rule_procs"), L.T("rule_procs_desc"));
        var tags = new ProcessTags(b.Kit.P, Cfg.Processes, L.T("add_app"), b.Fonts.Ctl, b.Surface);
        tags.Removed += name => { Cfg.Processes.Remove(name); Changed(); };
        tags.AddRequested += () => ShowAddMenu(tags);
        b.Add(tags, (int)Math.Ceiling(tags.Measure(b.Width) * 96.0 / DeviceDpi));
        b.EndSoft();

        b.Section(L.T("power"));
        b.Toggle(L.T("only_ac"), L.T("only_ac_desc"), Cfg.OnlyOnAC, v => { Cfg.OnlyOnAC = v; Changed(); });
        b.Toggle(string.Format(L.T("low_battery"), Cfg.LowBatteryPercent), L.T("low_battery_desc"), Cfg.PauseOnLowBattery, v => { Cfg.PauseOnLowBattery = v; Changed(); });
        b.Toggle(L.T("fullscreen"), L.T("fullscreen_desc"), Cfg.Fullscreen, v => { Cfg.Fullscreen = v; Changed(); });
    }

    void Changed()
    {
        Save();
        _app.Engine.Evaluate(force: true);
        RefreshPage();
    }

    void ShowAddMenu(Control anchor)
    {
        var menu = new ContextMenuStrip { Renderer = new MenuRenderer(K.P), Font = F.Ctl, ShowImageMargin = false };
        var browse = new ToolStripMenuItem(L.T("browse")) { Padding = new Padding(0, 4, 0, 4) };
        browse.Click += (_, _) =>
        {
            using var dlg = new OpenFileDialog { Filter = "*.exe|*.exe", Title = L.T("add_app").TrimStart('+', ' ') };
            if (dlg.ShowDialog(this) == DialogResult.OK) AddProcess(dlg.FileName);
        };
        menu.Items.Add(browse);

        var running = RunningApps();
        if (running.Count > 0)
        {
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem(L.T("running_apps")) { Enabled = false });
            foreach (var name in running)
            {
                var mi = new ToolStripMenuItem(name + ".exe") { Padding = new Padding(0, 2, 0, 2) };
                mi.Click += (_, _) => AddProcess(name);
                menu.Items.Add(mi);
            }
        }
        menu.Closed += (_, _) => BeginInvoke(menu.Dispose);
        var at = anchor.PointToClient(Cursor.Position);
        menu.Show(anchor, at);
    }

    void AddProcess(string name)
    {
        var n = Settings.Normalize(name);
        if (n.Length == 0 || Cfg.Processes.Contains(n)) return;
        Cfg.Processes.Add(n);
        Changed();
    }

    /// <summary>Penceresi açık programlar (listede olmayanlar), alfabetik.</summary>
    List<string> RunningApps()
    {
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        int self = Environment.ProcessId;
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (p.Id != self && p.MainWindowHandle != IntPtr.Zero && !Cfg.Processes.Contains(p.ProcessName.ToLowerInvariant()))
                    names.Add(p.ProcessName.ToLowerInvariant());
            }
            catch { }
            finally { p.Dispose(); }
        }
        return names.Take(20).ToList();
    }

    void Shortcut(PageBuilder b)
    {
        b.Section(L.T("hotkey_title"), L.T("hotkey_desc"));
        b.BeginSoft();
        var box = new HotkeyBox(b.Kit.P, Cfg.Hotkey, L.T("hotkey_prompt"), b.Fonts.Row, b.Surface)
        {
            CapsStyle = true,
            CapsFont = BrandFonts.Display(22 * DeviceDpi / 96f),
        };
        box.ComboChanged += c => { Cfg.Hotkey = c; Save(); RefreshPage(); };
        b.Add(box, 96);
        b.Gap(14);
        var change = b.Secondary(L.T("hotkey_change"), () => box.BeginCapture());
        b.Buttons(change);
        change.Left = (b.Width - change.Width) / 2 + change.Left;
        b.EndSoft();
        if (!_app.HotkeyRegistered && Cfg.HotkeyEnabled && (Cfg.Hotkey & 0xFFFF) != 0)
        {
            b.Gap(10);
            b.Note(L.T("hotkey_busy"), K.P.Warning);
        }
        b.Gap(6);
        b.Note(L.T("hotkey_hint"));
        b.Gap(8);
        b.Toggle(L.T("hotkey_enabled"), L.T("hotkey_enabled_desc"), Cfg.HotkeyEnabled, v => { Cfg.HotkeyEnabled = v; Save(); RefreshPage(); });
    }
}
