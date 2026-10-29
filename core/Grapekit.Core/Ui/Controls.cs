using System.Drawing.Drawing2D;

namespace Grapekit.Ui;

/// <summary>Temaya göre kendini çizen kontrollerin ortak tabanı.</summary>
public abstract class ThemedControl : Control
{
    protected Palette P;
    protected bool Hover;
    protected bool Pressed;
    protected Color Surface;

    protected ThemedControl(Palette p, Color? surface = null)
    {
        P = p;
        Surface = surface ?? p.Card;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
               | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        BackColor = Surface;
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    protected int S(int v) => LogicalToDeviceUnits(v);
    protected float Sf(float v) => v * DeviceDpi / 96f;

    protected Color Dim(Color c) => Enabled ? c : ThemeInfo.Mix(c, Surface, 0.6);

    protected Graphics Begin(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Surface);
        return g;
    }

    protected void FocusRing(Graphics g, float radius)
    {
        if (!Focused || !ShowFocusCues) return;
        var r = new RectangleF(1, 1, Width - 2.5f, Height - 2.5f);
        Draw.Stroke(g, r, radius, P.AccentText, Math.Max(1.5f, Sf(1.5f)));
    }

    protected override void OnMouseEnter(EventArgs e) { Hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { Hover = false; Pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { Pressed = true; Focus(); Invalidate(); } base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { Pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnEnabledChanged(EventArgs e)
    {
        Cursor = Enabled ? Cursors.Hand : Cursors.Default;
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected static void DrawText(Graphics g, string text, Font f, Rectangle r, Color c, TextFormatFlags extra = 0) =>
        TextRenderer.DrawText(g, text, f, r, c,
            TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | extra);

    protected static void Center(Graphics g, string text, Font f, Rectangle r, Color c) =>
        DrawText(g, text, f, r, c, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
}

/// <summary>Başlık + açıklama + sağda aç/kapa anahtarı. Tüm satır tıklanabilir.</summary>
public sealed class ToggleRow : ThemedControl
{
    readonly string _title, _desc;
    readonly Font _titleFont, _descFont;
    readonly System.Windows.Forms.Timer _anim = new() { Interval = 15 };
    float _k;
    bool _checked;

    public event Action<bool>? CheckedChanged;

    /// <summary>Metnin soldan boşluğu (mantıksal piksel).</summary>
    public int Inset { get; set; } = 12;

    public ToggleRow(Palette p, string title, string desc, bool value, Font titleFont, Font descFont, Color? surface = null) : base(p, surface)
    {
        _title = title; _desc = desc; _checked = value; _k = value ? 1 : 0;
        _titleFont = titleFont; _descFont = descFont;
        AccessibleName = title;
        AccessibleRole = AccessibleRole.CheckButton;
        _anim.Tick += (_, _) =>
        {
            float target = _checked ? 1 : 0;
            _k += Math.Sign(target - _k) * 0.2f;
            if (Math.Abs(_k - target) < 0.2f) { _k = target; _anim.Stop(); }
            Invalidate();
        };
    }

    public bool Checked
    {
        get => _checked;
        set { if (_checked == value) return; _checked = value; _anim.Start(); CheckedChanged?.Invoke(value); }
    }

    /// <summary>Olay tetiklemeden değeri değiştir (dışarıdan eşitleme).</summary>
    public void SetSilently(bool v) { _checked = v; _k = v ? 1 : 0; Invalidate(); }

    protected override void OnClick(EventArgs e) { base.OnClick(e); Checked = !Checked; }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter) { Checked = !Checked; e.Handled = true; }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = Begin(e);
        float rad = Sf(6);
        if (Hover && Enabled) Draw.Fill(g, new RectangleF(0, 0, Width, Height), rad, P.Hover);

        int sw = S(44), sh = S(26);
        var sr = new RectangleF(Width - S(Inset == 0 ? 2 : 14) - sw, (Height - sh) / 2f, sw, sh);

        int left = S(Inset), textW = (int)sr.Left - S(16) - left;
        int th = _titleFont.Height, dh = string.IsNullOrEmpty(_desc) ? 0 : _descFont.Height, gap = dh > 0 ? S(2) : 0;
        int top = (Height - (th + gap + dh)) / 2;
        DrawText(g, _title, _titleFont, new Rectangle(left, top, textW, th), Dim(P.Text));
        if (dh > 0) DrawText(g, _desc, _descFont, new Rectangle(left, top + th + gap, textW, dh), Dim(P.SubText));

        // Grapekit anahtarı: yuvarlak hat, üzüm kutucuğu gibi köşeli düğme
        Color track = ThemeInfo.Mix(Surface, P.Accent, _k);
        float bw = Math.Max(1.5f, Sf(2));
        Draw.Fill(g, sr, sh / 2f, Dim(track));
        Draw.Stroke(g, RectangleF.Inflate(sr, -bw / 2, -bw / 2), sh / 2f - bw / 2, Dim(ThemeInfo.Mix(P.SubText, P.Accent, _k)), bw);

        float d = Sf(14);
        float x0 = sr.Left + Sf(5), x1 = sr.Right - Sf(5) - d;
        var knob = new RectangleF(x0 + (x1 - x0) * _k, sr.Top + (sh - d) / 2f, d, d);
        Draw.Fill(g, knob, Sf(5), Dim(ThemeInfo.Mix(P.SubText, P.OnAccent, _k)));

        FocusRing(g, rad);
    }

    protected override void Dispose(bool disposing) { if (disposing) _anim.Dispose(); base.Dispose(disposing); }
}

/// <summary>Yan yana seçenekler (Sistem | Açık | Koyu gibi).</summary>
public sealed class Segmented : ThemedControl
{
    readonly string[] _options;
    int _selected, _hoverIndex = -1;

    public event Action<int>? SelectedChanged;

    public Segmented(Palette p, string[] options, int selected, Font font) : base(p)
    {
        _options = options; _selected = selected; Font = font;
        AccessibleRole = AccessibleRole.PageTabList;
    }

    public int Selected
    {
        get => _selected;
        set
        {
            value = Math.Clamp(value, 0, _options.Length - 1);
            if (value == _selected) return;
            _selected = value; Invalidate(); SelectedChanged?.Invoke(value);
        }
    }

    int IndexAt(int x) => Math.Clamp(x * _options.Length / Math.Max(1, Width), 0, _options.Length - 1);

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int i = IndexAt(e.X);
        if (i != _hoverIndex) { _hoverIndex = i; Invalidate(); }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e) { _hoverIndex = -1; base.OnMouseLeave(e); }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) Selected = IndexAt(e.X);
        base.OnMouseClick(e);
    }

    protected override bool IsInputKey(Keys k) => k is Keys.Left or Keys.Right || base.IsInputKey(k);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Left) Selected--;
        else if (e.KeyCode == Keys.Right) Selected++;
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = Begin(e);
        float rad = Sf(7);
        Draw.Fill(g, new RectangleF(0, 0, Width, Height), rad, P.Neutral);
        float w = Width / (float)_options.Length, inset = Sf(3);
        for (int i = 0; i < _options.Length; i++)
        {
            var r = new RectangleF(i * w + inset, inset, w - 2 * inset, Height - 2 * inset);
            Color fg = P.Text;
            if (i == _selected)
            {
                Draw.Fill(g, r, rad - inset / 2, Dim(P.Accent));
                fg = P.OnAccent;
            }
            else if (i == _hoverIndex && Enabled)
            {
                Draw.Fill(g, r, rad - inset / 2, ThemeInfo.Mix(P.Neutral, P.Text, 0.08));
            }
            Center(g, _options[i], Font, Rectangle.Round(r), Dim(fg));
        }
        FocusRing(g, rad);
    }
}

/// <summary>Düz, yuvarlak köşeli düğme. Text başında MDL2 simgesi olabilir (Icon).</summary>
public sealed class FlatButton : ThemedControl
{
    readonly bool _primary;
    public string? Icon { get; set; }

    public FlatButton(Palette p, string text, bool primary, Font font, Color? surface = null) : base(p, surface)
    {
        Text = text; _primary = primary; Font = font;
        AccessibleRole = AccessibleRole.PushButton;
        AccessibleName = text;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter) { OnClick(EventArgs.Empty); e.Handled = true; }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = Begin(e);
        Color bg = _primary ? P.Accent : P.Neutral;
        Color fg = _primary ? P.OnAccent : P.Text;
        Color toward = P.Dark || _primary ? Color.White : Color.Black;
        if (Enabled && Pressed) bg = ThemeInfo.Mix(bg, Color.Black, 0.12);
        else if (Enabled && Hover) bg = ThemeInfo.Mix(bg, toward, 0.10);
        float rad = Sf(7);
        Draw.Fill(g, new RectangleF(0, 0, Width, Height), rad, Dim(bg));
        if (P.HighContrast) Draw.Stroke(g, new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), rad, P.Text);

        if (Icon != null)
        {
            using var f = new Font(Draw.IconFont, Font.Size * 0.95f, FontStyle.Regular, GraphicsUnit.Pixel);
            var ts = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPrefix);
            int iw = S(18), total = iw + S(6) + ts.Width;
            int x = (Width - total) / 2;
            TextRenderer.DrawText(g, Icon, f, new Rectangle(x - S(4), 0, iw + S(8), Height), Dim(fg),
                TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            DrawText(g, Text, Font, new Rectangle(x + iw + S(6), 0, ts.Width + S(4), Height), Dim(fg), TextFormatFlags.VerticalCenter);
        }
        else Center(g, Text, Font, ClientRectangle, Dim(fg));
        FocusRing(g, rad);
    }
}

/// <summary>Küçük bir monitör çizimi üzerinde 3x3 konum seçici.</summary>
public sealed class PositionPicker : ThemedControl
{
    int _selected, _hoverIndex = -1;

    public event Action<int>? SelectedChanged;

    public PositionPicker(Palette p, int selected) : base(p)
    {
        _selected = selected;
        AccessibleRole = AccessibleRole.Grouping;
    }

    public int Selected
    {
        get => _selected;
        set
        {
            value = Math.Clamp(value, 0, 8);
            _selected = value; Invalidate(); SelectedChanged?.Invoke(value);
        }
    }

    RectangleF ScreenRect => new(Sf(2), Sf(2), Width - Sf(4), Height - Sf(4) - Sf(16));

    RectangleF Cell(int i)
    {
        var s = ScreenRect;
        float pad = Sf(8);
        float cw = (s.Width - 2 * pad) / 3, ch = (s.Height - 2 * pad) / 3;
        return new RectangleF(s.X + pad + (i % 3) * cw, s.Y + pad + (i / 3) * ch, cw, ch);
    }

    int IndexAt(Point pt)
    {
        for (int i = 0; i < 9; i++) if (Cell(i).Contains(pt)) return i;
        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int i = IndexAt(e.Location);
        if (i != _hoverIndex) { _hoverIndex = i; Invalidate(); }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e) { _hoverIndex = -1; base.OnMouseLeave(e); }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        int i = IndexAt(e.Location);
        if (e.Button == MouseButtons.Left && i >= 0) Selected = i;
        base.OnMouseClick(e);
    }

    protected override bool IsInputKey(Keys k) => k is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(k);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        int c = _selected % 3, r = _selected / 3;
        switch (e.KeyCode)
        {
            case Keys.Left when c > 0: Selected = _selected - 1; break;
            case Keys.Right when c < 2: Selected = _selected + 1; break;
            case Keys.Up when r > 0: Selected = _selected - 3; break;
            case Keys.Down when r < 2: Selected = _selected + 3; break;
            case Keys.Space or Keys.Enter: Selected = _selected; break;
        }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = Begin(e);
        var s = ScreenRect;
        float rad = Sf(8);

        float neckW = Sf(14), baseW = Sf(56);
        using (var b = new SolidBrush(Dim(P.Border)))
        {
            g.FillRectangle(b, s.X + (s.Width - neckW) / 2, s.Bottom, neckW, Sf(10));
            using var foot = Draw.Round(new RectangleF(s.X + (s.Width - baseW) / 2, s.Bottom + Sf(9), baseW, Sf(5)), Sf(2.5f));
            g.FillPath(b, foot);
        }

        Draw.Fill(g, s, rad, Dim(P.Dark ? ThemeInfo.C(0x1C1C1C) : ThemeInfo.C(0xF0F3F7)));
        Draw.Stroke(g, RectangleF.Inflate(s, -1, -1), rad - 1, Dim(P.Dark ? ThemeInfo.C(0x5A5A5A) : ThemeInfo.C(0xB8B8B8)), Math.Max(1.5f, Sf(2)));

        for (int i = 0; i < 9; i++)
        {
            var c = Cell(i);
            var chip = new RectangleF(c.X + c.Width * 0.18f, c.Y + c.Height * 0.25f, c.Width * 0.64f, c.Height * 0.5f);
            float cr = chip.Height / 2.6f;
            if (i == _selected)
            {
                Draw.Fill(g, chip, cr, Dim(P.Accent));
                var key = new RectangleF(chip.X + Sf(3), chip.Y + Sf(3), chip.Height - Sf(6), chip.Height - Sf(6));
                Draw.Glyph(g, "A", key, key.Height * 0.8f, Dim(P.OnAccent));
            }
            else if (i == _hoverIndex && Enabled)
                Draw.Fill(g, chip, cr, ThemeInfo.Mix(P.Accent, P.Card, 0.55));
            else
                Draw.Stroke(g, chip, cr, Dim(P.Dark ? ThemeInfo.C(0x4A4A4A) : ThemeInfo.C(0xC9CED6)), Math.Max(1f, Sf(1.2f)));
        }

        if (Focused && ShowFocusCues)
            Draw.Stroke(g, RectangleF.Inflate(Cell(_selected), -1, -1), Sf(4), P.AccentText, Math.Max(1.5f, Sf(1.5f)));
    }
}

/// <summary>[ − ]  değer  [ + ] biçiminde ayarlayıcı.</summary>
public sealed class Stepper : ThemedControl
{
    readonly int _min, _max, _step;
    readonly bool _wrap;
    readonly Func<int, string> _format;
    int _value, _hoverZone = -1;

    public event Action<int>? ValueChanged;

    /// <summary>İsteğe bağlı değişken adım: (değer, yön ±1) → yeni değer. Verilmezse sabit adım.</summary>
    public Func<int, int, int>? Next { get; set; }

    void Step(int dir) => Value = Next != null ? Next(_value, dir) : _value + dir * _step;

    public Stepper(Palette p, int value, int min, int max, int step, Func<int, string> format, Font font, bool wrap = false, Color? surface = null) : base(p, surface)
    {
        _value = value; _min = min; _max = max; _step = step; _format = format; _wrap = wrap; Font = font;
        AccessibleRole = AccessibleRole.SpinButton;
    }

    public int Value
    {
        get => _value;
        set
        {
            if (_wrap)
            {
                int span = _max - _min + _step;
                value = ((value - _min) % span + span) % span + _min;
            }
            else value = Math.Clamp(value, _min, _max);
            if (value == _value) return;
            _value = value; Invalidate(); ValueChanged?.Invoke(value);
        }
    }

    RectangleF Minus => new(0, 0, Height, Height);
    RectangleF Plus => new(Width - Height, 0, Height, Height);

    int ZoneAt(Point p) => Minus.Contains(p) ? 0 : Plus.Contains(p) ? 1 : -1;

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int z = ZoneAt(e.Location);
        if (z != _hoverZone) { _hoverZone = z; Invalidate(); }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e) { _hoverZone = -1; base.OnMouseLeave(e); }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            int z = ZoneAt(e.Location);
            if (z == 0) Step(-1); else if (z == 1) Step(1);
        }
        base.OnMouseClick(e);
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e) { OnMouseClick(e); base.OnMouseDoubleClick(e); }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (Enabled && Focused) Step(Math.Sign(e.Delta));
        base.OnMouseWheel(e);
    }

    protected override bool IsInputKey(Keys k) => k is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(k);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Left or Keys.Down or Keys.Subtract or Keys.OemMinus) Step(-1);
        else if (e.KeyCode is Keys.Right or Keys.Up or Keys.Add or Keys.Oemplus) Step(1);
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = Begin(e);
        float rad = Sf(7);
        Draw.Fill(g, new RectangleF(0, 0, Width, Height), rad, Dim(P.Neutral));

        void Btn(RectangleF r, bool plus, bool hover, bool enabled)
        {
            var inner = RectangleF.Inflate(r, -Sf(3), -Sf(3));
            if (hover && enabled && Enabled) Draw.Fill(g, inner, rad - Sf(2), ThemeInfo.Mix(P.Neutral, P.Text, 0.10));
            Color c = enabled ? Dim(P.Text) : ThemeInfo.Mix(P.Text, P.Neutral, 0.7);
            using var pen = new Pen(c, Math.Max(1.5f, Sf(1.8f))) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, l = Sf(5.5f);
            g.DrawLine(pen, cx - l, cy, cx + l, cy);
            if (plus) g.DrawLine(pen, cx, cy - l, cx, cy + l);
        }

        Btn(Minus, false, _hoverZone == 0, _wrap || _value > _min);
        Btn(Plus, true, _hoverZone == 1, _wrap || _value < _max);

        var mid = new Rectangle((int)Minus.Right, 0, (int)(Plus.Left - Minus.Right), Height);
        Center(g, _format(_value), Font, mid, Dim(P.Text));
        FocusRing(g, rad);
    }
}

/// <summary>Sürgü (ör. ses düzeyi, saydamlık). Sağda değeri yazar.</summary>
public sealed class Slider : ThemedControl
{
    readonly int _min, _max, _step;
    readonly Func<int, string> _format;
    int _value;
    bool _drag;

    public event Action<int>? ValueChanged;
    /// <summary>Sürükleme bittiğinde (önizleme için).</summary>
    public event Action<int>? Committed;

    public Slider(Palette p, int value, int min, int max, int step, Func<int, string> format, Font font) : base(p)
    {
        _value = value; _min = min; _max = max; _step = step; _format = format; Font = font;
        AccessibleRole = AccessibleRole.Slider;
    }

    public int Value
    {
        get => _value;
        set
        {
            value = Math.Clamp((int)Math.Round(value / (double)_step) * _step, _min, _max);
            if (value == _value) return;
            _value = value; Invalidate(); ValueChanged?.Invoke(value);
        }
    }

    int LabelW => S(52);
    RectangleF Track => new(S(10), Height / 2f - Sf(2), Width - LabelW - S(20), Sf(4));

    void SetFromX(int x)
    {
        var t = Track;
        double k = Math.Clamp((x - t.X) / t.Width, 0, 1);
        Value = (int)Math.Round(_min + k * (_max - _min));
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left) { _drag = true; SetFromX(e.X); }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_drag) SetFromX(e.X);
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (_drag) { _drag = false; Committed?.Invoke(_value); }
        base.OnMouseUp(e);
    }

    protected override bool IsInputKey(Keys k) => k is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(k);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        int old = _value;
        if (e.KeyCode is Keys.Left or Keys.Down) Value -= _step;
        else if (e.KeyCode is Keys.Right or Keys.Up) Value += _step;
        else if (e.KeyCode == Keys.Home) Value = _min;
        else if (e.KeyCode == Keys.End) Value = _max;
        if (old != _value) Committed?.Invoke(_value);
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = Begin(e);
        var t = Track;
        float k = (_value - _min) / (float)Math.Max(1, _max - _min);
        Draw.Fill(g, t, t.Height / 2, Dim(P.Dark ? ThemeInfo.C(0x5A5A5A) : ThemeInfo.C(0xC4C4C4)));
        Draw.Fill(g, new RectangleF(t.X, t.Y, t.Width * k, t.Height), t.Height / 2, Dim(P.Accent));

        float cx = t.X + t.Width * k, cy = t.Y + t.Height / 2, r = Sf(9);
        using (var b = new SolidBrush(Dim(P.Dark ? ThemeInfo.C(0x454545) : Color.White))) g.FillEllipse(b, cx - r, cy - r, 2 * r, 2 * r);
        using (var pen = new Pen(Dim(P.Border))) g.DrawEllipse(pen, cx - r, cy - r, 2 * r, 2 * r);
        float ir = Hover || _drag ? Sf(6) : Sf(4.5f);
        using (var b = new SolidBrush(Dim(P.Accent))) g.FillEllipse(b, cx - ir, cy - ir, 2 * ir, 2 * ir);

        DrawText(g, _format(_value), Font, new Rectangle(Width - LabelW, 0, LabelW, Height), Dim(P.Text),
            TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        if (Focused && ShowFocusCues)
            using (var pen = new Pen(P.AccentText, Sf(1.5f))) g.DrawEllipse(pen, cx - r - Sf(2), cy - r - Sf(2), 2 * r + Sf(4), 2 * r + Sf(4));
    }
}

/// <summary>Açılır liste: tıklayınca temalı menü açılır.</summary>
public sealed class DropDown : ThemedControl
{
    readonly string[] _options;
    int _selected;

    public event Action<int>? SelectedChanged;

    public DropDown(Palette p, string[] options, int selected, Font font, Color? surface = null) : base(p, surface)
    {
        _options = options; _selected = Math.Clamp(selected, 0, options.Length - 1); Font = font;
        AccessibleRole = AccessibleRole.ComboBox;
    }

    public int Selected => _selected;

    void Select(int i)
    {
        if (i == _selected) return;
        _selected = i; Invalidate(); SelectedChanged?.Invoke(i);
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        var menu = new ContextMenuStrip { Renderer = new MenuRenderer(P), Font = Font, ShowImageMargin = true };
        for (int i = 0; i < _options.Length; i++)
        {
            int idx = i;
            var mi = new ToolStripMenuItem(_options[i]) { Checked = i == _selected, Padding = new Padding(0, 3, 0, 3) };
            mi.Click += (_, _) => Select(idx);
            menu.Items.Add(mi);
        }
        menu.MinimumSize = new Size(Width, 0);
        menu.Closed += (_, _) => BeginInvoke(menu.Dispose);
        menu.Show(this, new Point(0, Height + S(2)));
    }

    protected override bool IsInputKey(Keys k) => k is Keys.Up or Keys.Down || base.IsInputKey(k);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Up) Select(Math.Max(0, _selected - 1));
        else if (e.KeyCode == Keys.Down) Select(Math.Min(_options.Length - 1, _selected + 1));
        else if (e.KeyCode is Keys.Space or Keys.Enter or Keys.F4) OnClick(EventArgs.Empty);
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = Begin(e);
        float rad = Sf(7);
        Color bg = Hover && Enabled ? ThemeInfo.Mix(P.Neutral, P.Text, 0.06) : P.Neutral;
        Draw.Fill(g, new RectangleF(0, 0, Width, Height), rad, Dim(bg));
        DrawText(g, _options[_selected], Font, new Rectangle(S(12), 0, Width - S(40), Height), Dim(P.Text), TextFormatFlags.VerticalCenter);
        using var f = new Font(Draw.IconFont, Sf(11), FontStyle.Regular, GraphicsUnit.Pixel);
        Center(g, "", f, new Rectangle(Width - S(32), 0, S(24), Height), Dim(P.SubText));
        FocusRing(g, rad);
    }
}

/// <summary>Kısayol tuşu yakalayıcı: tıklayın, tuş bileşimine basın.</summary>
public sealed class HotkeyBox : ThemedControl
{
    int _combo;
    bool _capturing;
    readonly string _prompt;

    public event Action<int>? ComboChanged;
    public event Action<bool>? CaptureChanged;

    /// <summary>Tuş kapakları görünümü (Grapekit ayarlar penceresi). CapsFont: kapak yazısı.</summary>
    public bool CapsStyle { get; set; }
    public Font? CapsFont { get; set; }
    public int Combo => _combo;
    public void BeginCapture() { Focus(); SetCapturing(true); }

    public HotkeyBox(Palette p, int combo, string prompt, Font font, Color? surface = null) : base(p, surface)
    {
        _combo = combo; _prompt = prompt; Font = font;
        AccessibleRole = AccessibleRole.HotkeyField;
    }

    void SetCapturing(bool v)
    {
        if (_capturing == v) return;
        _capturing = v;
        CaptureChanged?.Invoke(v);
        Invalidate();
    }

    protected override void OnClick(EventArgs e) { base.OnClick(e); Focus(); SetCapturing(true); }
    protected override void OnLostFocus(EventArgs e) { SetCapturing(false); base.OnLostFocus(e); }

    protected override bool IsInputKey(Keys k) => _capturing || base.IsInputKey(k);

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (_capturing && (msg.Msg == 0x100 || msg.Msg == 0x104)) { HandleKey(keyData); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    void HandleKey(Keys keyData)
    {
        var key = keyData & Keys.KeyCode;
        if (!_capturing)
        {
            if (key is Keys.Enter or Keys.Space) SetCapturing(true);
            return;
        }
        if (key == Keys.Escape) { SetCapturing(false); return; }
        if (key is Keys.Back or Keys.Delete) { _combo = 0; SetCapturing(false); ComboChanged?.Invoke(0); return; }
        if (key is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin or Keys.None) return;

        uint mods = 0;
        if ((keyData & Keys.Control) != 0) mods |= Win32.MOD_CONTROL;
        if ((keyData & Keys.Alt) != 0) mods |= Win32.MOD_ALT;
        if ((keyData & Keys.Shift) != 0) mods |= Win32.MOD_SHIFT;
        if ((Win32.GetKeyState(0x5B) & 0x8000) != 0 || (Win32.GetKeyState(0x5C) & 0x8000) != 0) mods |= Win32.MOD_WIN;
        if ((mods & (Win32.MOD_CONTROL | Win32.MOD_ALT | Win32.MOD_WIN)) == 0) return; // en az bir Ctrl/Alt/Win

        _combo = (int)(mods << 16 | (uint)key);
        SetCapturing(false);
        ComboChanged?.Invoke(_combo);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        HandleKey(e.KeyData);
        e.Handled = true;
        base.OnKeyDown(e);
    }

    void PaintCaps(Graphics g)
    {
        float rad = Sf(16);
        if (_capturing)
        {
            using var pen = new Pen(P.Accent, Sf(2)) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
            using var path = Draw.Round(new RectangleF(Sf(1), Sf(1), Width - Sf(2), Height - Sf(2)), rad);
            g.DrawPath(pen, path);
            Center(g, _prompt, Font, ClientRectangle, P.AccentText);
            return;
        }
        var font = CapsFont ?? Font;
        var parts = (_combo & 0xFFFF) == 0 ? ["—"] : HotkeyText.Describe(_combo).Split(" + ");
        int ch = S(56), gap = S(10), plusW = TextRenderer.MeasureText("+", font).Width;
        var widths = parts.Select(t => Math.Max(S(64), TextRenderer.MeasureText(t, font).Width + S(28))).ToArray();
        int total = widths.Sum() + (parts.Length - 1) * (plusW + gap * 2);
        int x = (Width - total) / 2, y = (Height - ch) / 2;
        for (int i = 0; i < parts.Length; i++)
        {
            bool last = i == parts.Length - 1 && parts.Length > 1;
            var r = new RectangleF(x, y, widths[i], ch);
            Color face = last ? P.Accent : P.Bg, edge = last ? ThemeInfo.Mix(P.Accent, Color.Black, 0.3) : P.Border;
            Draw.Fill(g, r, Sf(14), Dim(edge));
            Draw.Fill(g, new RectangleF(r.X + Sf(1), r.Y + Sf(1), r.Width - Sf(2), r.Height - Sf(4)), Sf(13), Dim(face));
            Center(g, parts[i], font, Rectangle.Round(new RectangleF(r.X, r.Y, r.Width, r.Height - Sf(3))), Dim(last ? P.OnAccent : P.Text));
            x += widths[i];
            if (i < parts.Length - 1)
            {
                Center(g, "+", font, new Rectangle(x + gap, y, plusW, ch), P.SubText);
                x += plusW + gap * 2;
            }
        }
        FocusRing(g, rad);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = Begin(e);
        if (CapsStyle) { PaintCaps(g); return; }
        float rad = Sf(7);
        Draw.Fill(g, new RectangleF(0, 0, Width, Height), rad, Dim(P.Neutral));
        if (_capturing)
        {
            Draw.Stroke(g, new RectangleF(1, 1, Width - 2, Height - 2), rad, P.Accent, Sf(2));
            Center(g, _prompt, Font, ClientRectangle, P.AccentText);
        }
        else
        {
            Center(g, HotkeyText.Describe(_combo), Font, ClientRectangle, Dim(P.Text));
            FocusRing(g, rad);
        }
    }
}

/// <summary>Renk seçici: Windows vurgu rengi, hazır renkler ve özel renk.</summary>
public sealed class ColorSwatches : ThemedControl
{
    int _argb, _hover = -2;

    public event Action<int>? ColorChanged;

    public ColorSwatches(Palette p, int argb) : base(p)
    {
        _argb = argb;
        AccessibleRole = AccessibleRole.List;
    }

    int Count => ThemeInfo.Swatches.Length + 2; // 0: Windows, son: özel
    int Cell => S(36);

    Rectangle CellRect(int i) => new(i * (Cell + S(6)), (Height - Cell) / 2, Cell, Cell);

    int IndexAt(Point p)
    {
        for (int i = 0; i < Count; i++) if (CellRect(i).Contains(p)) return i;
        return -2;
    }

    int SelectedIndex
    {
        get
        {
            if (_argb == 0) return 0;
            int i = Array.IndexOf(ThemeInfo.Swatches, _argb);
            return i >= 0 ? i + 1 : Count - 1;
        }
    }

    void Pick(int i)
    {
        if (i == 0) Set(0);
        else if (i <= ThemeInfo.Swatches.Length) Set(ThemeInfo.Swatches[i - 1]);
        else
        {
            using var dlg = new ColorDialog { FullOpen = true, AnyColor = true, Color = _argb == 0 ? ThemeInfo.SystemAccent() : Color.FromArgb(_argb) };
            if (dlg.ShowDialog(FindForm()) == DialogResult.OK) Set(Color.FromArgb(255, dlg.Color).ToArgb());
        }
    }

    void Set(int argb)
    {
        _argb = argb;
        Invalidate();
        ColorChanged?.Invoke(argb);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int i = IndexAt(e.Location);
        if (i != _hover) { _hover = i; Invalidate(); }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e) { _hover = -2; base.OnMouseLeave(e); }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        int i = IndexAt(e.Location);
        if (e.Button == MouseButtons.Left && i >= 0) Pick(i);
        base.OnMouseClick(e);
    }

    protected override bool IsInputKey(Keys k) => k is Keys.Left or Keys.Right || base.IsInputKey(k);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        int i = SelectedIndex;
        if (e.KeyCode == Keys.Left && i > 0) Pick(i - 1);
        else if (e.KeyCode == Keys.Right && i < Count - 2) Pick(i + 1);
        else if (e.KeyCode is Keys.Enter or Keys.Space) Pick(Count - 1);
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = Begin(e);
        int sel = SelectedIndex;
        using var icon = new Font(Draw.IconFont, Sf(14), FontStyle.Regular, GraphicsUnit.Pixel);
        for (int i = 0; i < Count; i++)
        {
            var r = CellRect(i);
            var inner = RectangleF.Inflate(r, -Sf(4), -Sf(4));
            if (i == 0)
            {
                // Windows vurgu rengi: dört renkli küçük pencere simgesi yerine vurgu rengi + "W"
                using (var b = new SolidBrush(ThemeInfo.SystemAccent())) g.FillEllipse(b, inner);
                Center(g, "", icon, Rectangle.Round(inner), Color.White);
            }
            else if (i == Count - 1)
            {
                Color custom = sel == i ? Color.FromArgb(_argb) : P.Neutral;
                using (var b = new SolidBrush(custom)) g.FillEllipse(b, inner);
                Center(g, "", icon, Rectangle.Round(inner), sel == i ? (ThemeInfo.Luminance(custom) > 0.55 ? Color.Black : Color.White) : P.Text);
            }
            else
            {
                using var b = new SolidBrush(Color.FromArgb(ThemeInfo.Swatches[i - 1]));
                g.FillEllipse(b, inner);
            }

            if (i == sel)
                using (var pen = new Pen(P.Text, Sf(2))) g.DrawEllipse(pen, RectangleF.Inflate(r, -Sf(1), -Sf(1)));
            else if (i == _hover)
                using (var pen = new Pen(P.SubText, Sf(1.5f))) g.DrawEllipse(pen, RectangleF.Inflate(r, -Sf(1), -Sf(1)));
        }
        if (Focused && ShowFocusCues)
            Draw.Stroke(g, RectangleF.Inflate(CellRect(sel), Sf(1), Sf(1)), Sf(20), P.AccentText, Sf(1.5f));
    }
}

/// <summary>Bildirim kartının canlı önizlemesi (masaüstü benzeri zemin üzerinde).</summary>
public sealed class BarChart : ThemedControl
{
    public string[] Labels { get; set; } = [];
    public int[] Primary { get; set; } = [];
    public int[] Secondary { get; set; } = [];
    public Color SecondaryColor { get; set; }

    public BarChart(Palette p, Font font) : base(p) { Font = font; TabStop = false; Cursor = Cursors.Default; SecondaryColor = p.Warning; }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = Begin(e);
        int n = Labels.Length;
        if (n == 0) return;
        int labelH = Font.Height + S(4), valueH = Font.Height + S(2);
        var area = new Rectangle(S(4), valueH, Width - S(8), Height - labelH - valueH);
        int max = Math.Max(1, Primary.DefaultIfEmpty(0).Max());
        float slot = area.Width / (float)n, bw = Math.Min(slot * 0.62f, Sf(28));

        using (var pen = new Pen(P.Border)) g.DrawLine(pen, area.Left, area.Bottom, area.Right, area.Bottom);

        for (int i = 0; i < n; i++)
        {
            float x = area.Left + slot * i + (slot - bw) / 2;
            int v = i < Primary.Length ? Primary[i] : 0;
            int s2 = i < Secondary.Length ? Secondary[i] : 0;
            float h = area.Height * v / (float)max;
            if (v > 0)
            {
                Draw.Fill(g, new RectangleF(x, area.Bottom - h, bw, h), Math.Min(Sf(4), h / 2), P.Accent);
                float h2 = area.Height * Math.Min(s2, v) / (float)max;
                if (h2 > 0) Draw.Fill(g, new RectangleF(x, area.Bottom - h2, bw, h2), Math.Min(Sf(4), h2 / 2), SecondaryColor);
                Center(g, v.ToString(), Font, new Rectangle((int)(x - slot), (int)(area.Bottom - h - valueH), (int)(bw + 2 * slot), valueH), P.SubText);
            }
            Center(g, Labels[i], Font, new Rectangle((int)(area.Left + slot * i), area.Bottom + S(2), (int)slot, labelH), P.SubText);
        }
    }
}

/// <summary>Sol menüdeki sekme düğmesi.</summary>
public sealed class NavButton : ThemedControl
{
    readonly string _icon;
    bool _selected;

    public NavButton(Palette p, string icon, string text, Font font, Color surface) : base(p, surface)
    {
        _icon = icon; Text = text; Font = font;
        AccessibleRole = AccessibleRole.PageTab;
        AccessibleName = text;
    }

    public bool Selected
    {
        get => _selected;
        set { _selected = value; Invalidate(); }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter) { OnClick(EventArgs.Empty); e.Handled = true; }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = Begin(e);
        float rad = Sf(6);
        var r = new RectangleF(0, 0, Width, Height);
        if (_selected) Draw.Fill(g, r, rad, P.Dark ? ThemeInfo.C(0x2D2D2D) : ThemeInfo.C(0xEAEAEA));
        else if (Hover) Draw.Fill(g, r, rad, P.Dark ? ThemeInfo.C(0x292929) : ThemeInfo.C(0xEDEDED));
        if (P.HighContrast && _selected) Draw.Stroke(g, RectangleF.Inflate(r, -1, -1), rad, P.Accent, 2);

        if (_selected)
            Draw.Fill(g, new RectangleF(0, Height * 0.28f, Sf(3), Height * 0.44f), Sf(1.5f), P.Accent);

        using var f = new Font(Draw.IconFont, Sf(16), FontStyle.Regular, GraphicsUnit.Pixel);
        Center(g, _icon, f, new Rectangle(S(10), 0, S(24), Height), _selected ? P.AccentText : P.Text);
        DrawText(g, Text, Font, new Rectangle(S(44), 0, Width - S(48), Height), P.Text, TextFormatFlags.VerticalCenter);
        FocusRing(g, rad);
    }
}
