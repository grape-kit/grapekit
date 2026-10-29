using System.Drawing.Drawing2D;

namespace KeepAwake;

/// <summary>Panelin mor bandındaki büyük halka: ortası aç/kapa düğmesi, halka kalan süre; altında sayaç.</summary>
internal sealed class RingHero : ThemedControl
{
    readonly Kit _k;
    readonly Font _big, _sub;
    public bool On { get; set; }
    public double Fraction { get; set; }
    public string BigText { get; set; } = "";
    public string SubText { get; set; } = "";
    public event Action? Toggled;

    public RingHero(Kit k) : base(k.P, k.Hero)
    {
        _k = k;
        float s = DeviceDpi / 96f;
        _big = BrandFonts.Display(40 * s);
        _sub = BrandFonts.Body(13 * s);
        AccessibleRole = AccessibleRole.PushButton;
        AccessibleName = L.T("toggle_name");
        Cursor = Cursors.Default;
    }

    RectangleF Ring => new((Width - Sf(168)) / 2f, 0, Sf(168), Sf(168));

    bool OverRing(Point p)
    {
        var r = Ring;
        float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, dx = p.X - cx, dy = p.Y - cy;
        return dx * dx + dy * dy <= r.Width * r.Width / 4;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        bool over = OverRing(e.Location);
        Cursor = over ? Cursors.Hand : Cursors.Default;
        if (over != Hover) { Hover = over; Invalidate(); }
    }

    protected override void OnMouseEnter(EventArgs e) { }
    protected override void OnMouseClick(MouseEventArgs e) { if (e.Button == MouseButtons.Left && OverRing(e.Location)) Toggled?.Invoke(); base.OnMouseClick(e); }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter) { Toggled?.Invoke(); e.Handled = true; }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = Begin(e);
        var r = Ring;
        float w = Sf(8);
        var arc = RectangleF.Inflate(r, -w / 2 - Sf(4), -w / 2 - Sf(4));
        using (var track = new Pen(ThemeInfo.Mix(_k.Hero, Color.White, 0.12), w)) g.DrawEllipse(track, arc);
        float sweep = (float)(360 * Math.Clamp(Fraction, 0, 1));
        if (sweep > 0.5f)
        {
            using var pen = new Pen(_k.Ring, w) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            if (sweep >= 359.5f) g.DrawEllipse(pen, arc);
            else g.DrawArc(pen, arc, -90, sweep);
        }

        var core = RectangleF.Inflate(r, -Sf(30), -Sf(30));
        Color fill = On ? _k.P.Accent : ThemeInfo.Mix(_k.Hero, Color.White, 0.10);
        if (Hover) fill = ThemeInfo.Mix(fill, Color.White, 0.12);
        Draw.Fill(g, core, Sf(36), fill);
        Draw.Glyph(g, "", core, Sf(40), Color.White); // güç simgesi
        if (Focused && ShowFocusCues) Draw.Stroke(g, RectangleF.Inflate(core, Sf(4), Sf(4)), Sf(40), _k.Ring, Sf(1.5f));

        int ty = (int)(r.Bottom + Sf(14));
        Center(g, BigText, _big, new Rectangle(0, ty, Width, _big.Height), _k.HeroText);
        Center(g, SubText, _sub, new Rectangle(0, ty + _big.Height + S(4), Width, _sub.Height + S(2)), _k.HeroSub);
    }

    protected override void Dispose(bool disposing) { if (disposing) { _big.Dispose(); _sub.Dispose(); } base.Dispose(disposing); }
}

/// <summary>Başlık + açıklama solda, açılır seçim sağda (ör. "Süre bitince: Uykuya al").</summary>
internal sealed class DropRow : ThemedControl
{
    readonly string _title;
    string _desc;
    readonly Font _tf, _df;
    public readonly DropDown Drop;

    public DropRow(Palette p, string title, string desc, string[] options, int selected, Font title_, Font desc_, Font ctl, Color surface) : base(p, surface)
    {
        _title = title; _desc = desc; _tf = title_; _df = desc_;
        TabStop = false; Cursor = Cursors.Default;
        Drop = new DropDown(p, options, selected, ctl, p.Neutral) { AccessibleName = title };
        Controls.Add(Drop);
    }

    public string Desc { get => _desc; set { if (_desc == value) return; _desc = value; Invalidate(); } }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int w = S(156), h = S(38);
        Drop.SetBounds(Width - w, (Height - h) / 2, w, h);
    }

    protected override void OnEnabledChanged(EventArgs e) { Drop.Enabled = Enabled; base.OnEnabledChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = Begin(e);
        int tw = Width - S(156) - S(12), th = _tf.Height, dh = _df.Height, top = (Height - th - dh - S(2)) / 2;
        DrawText(g, _title, _tf, new Rectangle(0, top, tw, th), Dim(P.Text));
        DrawText(g, _desc, _df, new Rectangle(0, top + th + S(2), tw, dh), Dim(P.SubText));
    }
}

/// <summary>Uyku Engelleyici'nin tepsi paneli.</summary>
internal sealed class AwakeFlyout : FlyoutWindow
{
    readonly AwakeEngine _e;
    readonly Settings _s;
    readonly RingHero _hero;
    readonly ChipGroup _chips;
    readonly DurationBox _custom;
    readonly ToggleRow _screen;
    readonly DropRow _end;
    readonly Action _save;

    public AwakeFlyout(Kit k, AwakeEngine engine, Settings s, Action save) : base(k)
    {
        _e = engine; _s = s; _save = save;
        float dpi = DeviceDpi / 96f;
        _hero = new RingHero(k);
        _hero.Toggled += () => _e.Toggle();
        SetHero(_hero, 266);

        AddLabel(L.T("duration"));
        _chips = new ChipGroup(k.P, L.DurationChips, L.ChipIndex(s), BrandFonts.BodySemi(12 * dpi), uniform: true, k.P.Bg);
        _chips.SelectedChanged += i =>
        {
            if (i == L.Durations.Length) _e.SetCustom(_s.CustomSeconds);
            else _e.SetDuration(L.Durations[i]);
            _save();
            SetBodyVisible(_custom!, _s.Custom);
            if (_s.Custom) _custom!.Focus();
        };
        AddBody(_chips, 44, gap: 6);

        _custom = new DurationBox(k.P, s.CustomSeconds, [L.T("unit_h"), L.T("unit_m"), L.T("unit_s")],
            BrandFonts.Display(18 * dpi), BrandFonts.BodySemi(13 * dpi), k.P.Bg);
        _custom.ValueChanged += v => { _e.SetCustom(v); _save(); };
        AddBody(_custom, 48, gap: 10);
        SetBodyVisible(_custom, s.Custom);

        var title = BrandFonts.BodySemi(14 * dpi);
        var desc = BrandFonts.Body(12 * dpi);
        _screen = new ToggleRow(k.P, L.T("keep_screen"), L.T("keep_screen_desc"), s.Mode == 1, title, desc, k.P.Bg) { Inset = 0 };
        _screen.CheckedChanged += v => { _s.Mode = v ? 1 : 0; _e.Apply(); _save(); };
        AddBody(_screen, 58, gap: 12);

        _end = new DropRow(k.P, L.T("end_action"), "", L.EndActions, s.EndAction, title, desc, BrandFonts.BodySemi(13 * dpi), k.P.Bg);
        _end.Drop.SelectedChanged += i => { _s.EndAction = i; _save(); };
        AddBody(_end, 58);
        Sync();
    }

    /// <summary>Motorun durumunu panele yansıtır (her saniye ve her değişiklikte).</summary>
    public void Sync()
    {
        _hero.On = _e.Awake;
        _hero.Fraction = _e.Fraction;
        var (big, sub) = Texts(_e);
        _hero.BigText = big;
        _hero.SubText = sub;
        _hero.Invalidate();
        StatusOn = _e.Awake;
        StatusText = _e.Awake ? (_e.Reason == Reason.Manual ? L.T("awake") : L.T("auto")) : _e.Paused ? L.T("paused") : L.T("off");
        Hint = _s.HotkeyEnabled && (_s.Hotkey & 0xFFFF) != 0 ? HotkeyText.Describe(_s.Hotkey).Replace(" + ", "+") : "";
        _chips.Selected = L.ChipIndex(_s);
        if (!_custom.ContainsFocus) _custom.Value = _s.CustomSeconds;
        _screen.SetSilently(_s.Mode == 1);
        bool timed = _s.EffectiveSeconds > 0;
        _end.Enabled = timed;
        _end.Desc = timed ? "" : L.T("end_action_off");
    }

    public static (string Big, string Sub) Texts(AwakeEngine e)
    {
        if (e.Paused) return (L.T("paused"), L.T(e.PausedForLowBattery ? "paused_low" : "paused_ac"));
        switch (e.Reason)
        {
            case Reason.Manual when e.Until is { } u:
                var t = e.Remaining;
                string clock = $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}";
                string at = u.ToLocalTime().ToString("HH:mm");
                int a = e.Settings.EndAction;
                return (clock, a > 0 ? string.Format(L.T("left_action"), at, L.T("end_verb" + a)) : string.Format(L.T("left"), at));
            case Reason.Manual: return (L.T("indefinite"), L.T("until_off"));
            case Reason.Process: return (L.T("auto"), string.Format(L.T("because_proc"), e.ProcessName));
            case Reason.Fullscreen: return (L.T("auto"), L.T("because_fs"));
            default: return (L.T("off"), L.T("sleep_normal"));
        }
    }
}
