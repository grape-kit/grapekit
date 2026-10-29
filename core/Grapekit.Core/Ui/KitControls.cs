namespace Grapekit.Ui;

/// <summary>Yuvarlak köşeli kare "çip" seçenekleri (ör. süre seçimi). uniform: genişliği eşit paylaştır.</summary>
public sealed class ChipGroup : ThemedControl
{
    readonly string[] _opts;
    readonly bool _uniform;
    int _sel, _hover = -1;

    public event Action<int>? SelectedChanged;

    public ChipGroup(Palette p, string[] options, int selected, Font font, bool uniform, Color? surface = null) : base(p, surface)
    {
        _opts = options; _sel = selected; _uniform = uniform; Font = font;
        AccessibleRole = AccessibleRole.List;
    }

    public int Selected
    {
        get => _sel;
        set { if (_sel == value) return; _sel = value; Invalidate(); }
    }

    RectangleF[] Rects()
    {
        int n = _opts.Length;
        float gap = Sf(6);
        var r = new RectangleF[n];
        if (_uniform)
        {
            float w = (Width - gap * (n - 1)) / n;
            for (int i = 0; i < n; i++) r[i] = new RectangleF(i * (w + gap), 0, w, Height);
            return r;
        }
        float x = 0;
        for (int i = 0; i < n; i++)
        {
            float w = Math.Max(Sf(76), TextRenderer.MeasureText(_opts[i], Font).Width + Sf(28));
            r[i] = new RectangleF(x, 0, w, Height);
            x += w + gap;
        }
        return r;
    }

    int Hit(Point pt) => Array.FindIndex(Rects(), r => r.Contains(pt));

    void Pick(int i)
    {
        if (i < 0 || i >= _opts.Length) return;
        bool changed = i != _sel;
        _sel = i;
        Invalidate();
        if (changed) SelectedChanged?.Invoke(i);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int h = Hit(e.Location);
        if (h != _hover) { _hover = h; Invalidate(); }
        Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e) { _hover = -1; base.OnMouseLeave(e); }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) Pick(Hit(e.Location));
        base.OnMouseClick(e);
    }

    protected override bool IsInputKey(Keys k) => k is Keys.Left or Keys.Right || base.IsInputKey(k);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Left) Pick(Math.Max(0, _sel - 1));
        else if (e.KeyCode == Keys.Right) Pick(Math.Min(_opts.Length - 1, _sel + 1));
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = Begin(e);
        var rects = Rects();
        float rad = Sf(12);
        for (int i = 0; i < rects.Length; i++)
        {
            var r = rects[i];
            bool sel = i == _sel;
            Color bg = sel ? P.Accent : i == _hover ? ThemeInfo.Mix(P.Neutral, P.Accent, 0.10) : P.Neutral;
            Draw.Fill(g, r, rad, Dim(bg));
            if (!sel) Draw.Stroke(g, RectangleF.Inflate(r, -0.5f, -0.5f), rad, Dim(P.Border));
            Center(g, _opts[i], Font, Rectangle.Round(r), Dim(sel ? P.OnAccent : P.Text));
            if (sel && Focused && ShowFocusCues)
                Draw.Stroke(g, RectangleF.Inflate(r, -Sf(3), -Sf(3)), rad - Sf(3), P.OnAccent, Sf(1.5f));
        }
    }
}

/// <summary>Simgeli, başlıklı ve açıklamalı seçim kartları (ör. çalışma modu). Biri seçilidir.</summary>
public sealed class ChoiceCards : ThemedControl
{
    readonly Kit _k;
    readonly (string Icon, string Title, string Desc)[] _items;
    readonly Font _desc;
    int _sel, _hover = -1;

    public event Action<int>? SelectedChanged;

    public ChoiceCards(Kit k, (string Icon, string Title, string Desc)[] items, int selected, Font title, Font desc, Color? surface = null)
        : base(k.P, surface ?? k.P.Bg)
    {
        _k = k; _items = items; _sel = selected; Font = title; _desc = desc;
        AccessibleRole = AccessibleRole.List;
    }

    public int Selected => _sel;

    RectangleF Rect(int i)
    {
        float gap = Sf(12), w = (Width - gap * (_items.Length - 1)) / _items.Length;
        return new RectangleF(i * (w + gap), 0, w, Height);
    }

    int Hit(Point pt) { for (int i = 0; i < _items.Length; i++) if (Rect(i).Contains(pt)) return i; return -1; }

    void Pick(int i)
    {
        if (i < 0 || i >= _items.Length || i == _sel) return;
        _sel = i; Invalidate(); SelectedChanged?.Invoke(i);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int h = Hit(e.Location);
        if (h != _hover) { _hover = h; Invalidate(); }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e) { _hover = -1; base.OnMouseLeave(e); }
    protected override void OnMouseClick(MouseEventArgs e) { if (e.Button == MouseButtons.Left) Pick(Hit(e.Location)); base.OnMouseClick(e); }
    protected override bool IsInputKey(Keys k) => k is Keys.Left or Keys.Right || base.IsInputKey(k);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Left) Pick(_sel - 1);
        else if (e.KeyCode == Keys.Right) Pick(_sel + 1);
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = Begin(e);
        float rad = Sf(16), bw = Sf(2);
        for (int i = 0; i < _items.Length; i++)
        {
            var r = Rect(i);
            bool sel = i == _sel;
            var (icon, title, desc) = _items[i];
            Draw.Fill(g, r, rad, sel ? _k.AccentSoft : i == _hover ? P.Hover : Surface);
            Draw.Stroke(g, RectangleF.Inflate(r, -bw / 2, -bw / 2), rad, sel ? P.Accent : P.Border, bw);
            if (sel && Focused && ShowFocusCues) Draw.Stroke(g, RectangleF.Inflate(r, -bw * 2, -bw * 2), rad - bw, P.AccentText, Sf(1));

            var tile = new RectangleF(r.X + Sf(16), r.Y + Sf(16), Sf(40), Sf(40));
            Draw.Fill(g, tile, Sf(13), sel ? P.Accent : P.Neutral);
            Draw.Glyph(g, icon, tile, Sf(19), sel ? P.OnAccent : P.Text);

            int tx = (int)(tile.Right + Sf(14)), tw = (int)(r.Right - Sf(16)) - tx;
            DrawText(g, title, Font, new Rectangle(tx, (int)(r.Y + Sf(16)), tw, Font.Height), P.Text);
            TextRenderer.DrawText(g, desc, _desc, new Rectangle(tx, (int)(r.Y + Sf(18)) + Font.Height, tw, (int)r.Height - Font.Height - S(26)),
                P.SubText, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
        }
    }
}

/// <summary>Yuvarlak köşeli düğme; renkleri dışarıdan verilir (birincil, ikincil, bağlantı, mor bant üstü).</summary>
public sealed class PillButton : ThemedControl
{
    public Color Fill { get; set; }
    public Color Fore { get; set; }
    public Color? Line { get; set; }
    public string? Icon { get; set; }

    public PillButton(Palette p, string text, Font font, Color surface, Color fill, Color fore, Color? line = null) : base(p, surface)
    {
        Text = text; Font = font; Fill = fill; Fore = fore; Line = line;
        AccessibleRole = AccessibleRole.PushButton;
        AccessibleName = text;
    }

    public static PillButton Primary(Kit k, string text, Font f, Color surface) => new(k.P, text, f, surface, k.P.Accent, k.P.OnAccent);
    public static PillButton Secondary(Kit k, string text, Font f, Color surface) => new(k.P, text, f, surface, surface, k.P.Text, k.P.Border);
    public static PillButton Link(Kit k, string text, Font f, Color surface) => new(k.P, text, f, surface, surface, k.P.AccentText);

    public int PreferredWidth => TextRenderer.MeasureText(Text, Font).Width + S(Icon != null ? 58 : 36);

    protected override void OnTextChanged(EventArgs e) { AccessibleName = Text; Invalidate(); base.OnTextChanged(e); }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter) { OnClick(EventArgs.Empty); e.Handled = true; }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = Begin(e);
        Color bg = Fill;
        Color toward = ThemeInfo.Luminance(Fill) > 0.5 ? Color.Black : Color.White;
        if (Enabled && Pressed) bg = ThemeInfo.Mix(bg, toward, 0.16);
        else if (Enabled && Hover) bg = Fill == Surface ? ThemeInfo.Mix(Surface, P.Accent, 0.08) : ThemeInfo.Mix(bg, toward, 0.10);
        float rad = Sf(12);
        var r = new RectangleF(0, 0, Width, Height);
        Draw.Fill(g, r, rad, Dim(bg));
        if (Line is { } l) Draw.Stroke(g, RectangleF.Inflate(r, -0.5f, -0.5f), rad, Dim(l));
        if (Icon != null)
        {
            int tw = TextRenderer.MeasureText(Text, Font).Width, iw = S(16), gap = S(8);
            int x = (Width - (iw + gap + tw)) / 2;
            Draw.Glyph(g, Icon, new RectangleF(x, 0, iw, Height), Sf(14), Dim(Fore));
            DrawText(g, Text, Font, new Rectangle(x + iw + gap, 0, tw + S(4), Height), Dim(Fore), TextFormatFlags.VerticalCenter);
        }
        else Center(g, Text, Font, ClientRectangle, Dim(Fore));
        FocusRing(g, rad);
    }
}

/// <summary>Mor bandın altındaki sekmeler. Seçili sekmenin altında lila çizgi.</summary>
public sealed class TabStrip : ThemedControl
{
    readonly Kit _k;
    readonly string[] _tabs;
    int _sel, _hover = -1;

    public event Action<int>? SelectedChanged;

    public TabStrip(Kit k, string[] tabs, int selected, Font font) : base(k.P, k.Hero)
    {
        _k = k; _tabs = tabs; _sel = selected; Font = font;
        AccessibleRole = AccessibleRole.PageTabList;
    }

    public int Selected
    {
        get => _sel;
        set { if (_sel == value) return; _sel = value; Invalidate(); }
    }

    Rectangle Rect(int i)
    {
        int x = 0;
        for (int j = 0; j < i; j++) x += TextRenderer.MeasureText(_tabs[j], Font).Width + S(28);
        return new Rectangle(x, 0, TextRenderer.MeasureText(_tabs[i], Font).Width + S(28), Height);
    }

    int Hit(Point p) { for (int i = 0; i < _tabs.Length; i++) if (Rect(i).Contains(p)) return i; return -1; }

    void Pick(int i)
    {
        if (i < 0 || i >= _tabs.Length || i == _sel) return;
        _sel = i; Invalidate(); SelectedChanged?.Invoke(i);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int h = Hit(e.Location);
        if (h != _hover) { _hover = h; Invalidate(); }
        Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e) { _hover = -1; base.OnMouseLeave(e); }
    protected override void OnMouseClick(MouseEventArgs e) { if (e.Button == MouseButtons.Left) Pick(Hit(e.Location)); base.OnMouseClick(e); }
    protected override bool IsInputKey(Keys k) => k is Keys.Left or Keys.Right || base.IsInputKey(k);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Left) Pick(_sel - 1);
        else if (e.KeyCode == Keys.Right) Pick(_sel + 1);
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = Begin(e);
        for (int i = 0; i < _tabs.Length; i++)
        {
            var r = Rect(i);
            bool sel = i == _sel;
            Color fg = sel || i == _hover ? _k.HeroText : _k.HeroSub;
            Center(g, _tabs[i], Font, new Rectangle(r.X, 0, r.Width, r.Height - S(3)), fg);
            if (sel) Draw.Fill(g, new RectangleF(r.X + Sf(10), Height - Sf(3), r.Width - Sf(20), Sf(3)), Sf(1.5f), _k.Ring);
            if (sel && Focused && ShowFocusCues) Draw.Stroke(g, new RectangleF(r.X + 1, Sf(6), r.Width - 2, Height - Sf(12)), Sf(8), _k.Ring);
        }
    }
}

/// <summary>Kendi başlık çubuğumuzdaki küçült / kapat düğmesi.</summary>
public sealed class CaptionButton : ThemedControl
{
    readonly Kit _k;
    readonly string _glyph;
    readonly bool _close;

    public CaptionButton(Kit k, string glyph, bool close, string name) : base(k.P, k.Hero)
    {
        _k = k; _glyph = glyph; _close = close;
        TabStop = false; Cursor = Cursors.Default;
        AccessibleName = name; AccessibleRole = AccessibleRole.PushButton;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = Begin(e);
        if (Hover) g.Clear(_close ? ThemeInfo.C(0xC42B1C) : _k.HeroHover);
        using var f = new Font(Draw.IconFont, Sf(10), FontStyle.Regular, GraphicsUnit.Pixel);
        Center(g, _glyph, f, ClientRectangle, _k.HeroText);
    }
}

/// <summary>
/// Serbest süre girişi: saat : dakika : saniye kutularına doğrudan yazılır (fare tekerleği ve ↑/↓ da çalışır).
/// "90 dk" gibi taşan değerler bitince "1 sa 30 dk" olarak düzenlenir. Değer saniye cinsindendir (en az 1).
/// </summary>
public sealed class DurationBox : ThemedControl
{
    readonly TextBox[] _boxes = new TextBox[3];
    readonly string[] _units;
    readonly Font _unitFont;
    readonly System.Windows.Forms.Timer _debounce = new() { Interval = 700 };
    int _seconds;
    bool _sync;

    /// <summary>Toplam saniye; yazmaya ara verince, Enter'a basınca veya kutudan çıkınca.</summary>
    public event Action<int>? ValueChanged;

    public DurationBox(Palette p, int seconds, string[] units, Font digits, Font unitFont, Color? surface = null) : base(p, surface)
    {
        _units = units; _unitFont = unitFont;
        TabStop = false;
        Cursor = Cursors.IBeam;
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            var tb = new TextBox
            {
                BorderStyle = BorderStyle.None, TextAlign = HorizontalAlignment.Center, MaxLength = 4,
                Font = digits, BackColor = p.Neutral, ForeColor = p.Text, AccessibleName = units[i],
            };
            tb.KeyPress += (_, e) => { if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true; };
            tb.KeyDown += (_, e) => OnBoxKey(idx, e);
            tb.MouseWheel += (_, e) => Bump(idx, Math.Sign(e.Delta));
            tb.TextChanged += (_, _) => { if (!_sync) { _debounce.Stop(); _debounce.Start(); } };
            tb.Enter += (_, _) => { Invalidate(); BeginInvoke(tb.SelectAll); };
            tb.Leave += (_, _) => { Commit(); Invalidate(); };
            _boxes[i] = tb;
            Controls.Add(tb);
        }
        _debounce.Tick += (_, _) => { _debounce.Stop(); Commit(normalize: false); };
        Value = seconds;
    }

    public int Value
    {
        get => _seconds;
        set
        {
            _seconds = Math.Max(1, value);
            _sync = true;
            _boxes[0].Text = (_seconds / 3600).ToString();
            _boxes[1].Text = (_seconds / 60 % 60).ToString("00");
            _boxes[2].Text = (_seconds % 60).ToString("00");
            _sync = false;
        }
    }

    int Parse()
    {
        long total = 0;
        int[] mul = [3600, 60, 1];
        for (int i = 0; i < 3; i++) if (long.TryParse(_boxes[i].Text, out long v)) total += v * mul[i];
        return (int)Math.Min(total, int.MaxValue / 2);
    }

    void Commit(bool normalize = true)
    {
        _debounce.Stop();
        int v = Parse();
        if (v <= 0) { if (normalize) Value = _seconds; return; }
        bool changed = v != _seconds;
        _seconds = v;
        if (normalize) Value = v;
        if (changed) ValueChanged?.Invoke(v);
    }

    void Bump(int idx, int dir)
    {
        int[] mul = [3600, 60, 1];
        int v = Math.Max(1, Parse() + dir * mul[idx]);
        Value = v;
        _boxes[idx].SelectAll();
        _seconds = v;
        ValueChanged?.Invoke(v);
    }

    void OnBoxKey(int idx, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Up) { Bump(idx, 1); e.Handled = e.SuppressKeyPress = true; }
        else if (e.KeyCode == Keys.Down) { Bump(idx, -1); e.Handled = e.SuppressKeyPress = true; }
        else if (e.KeyCode == Keys.Enter) { Commit(); e.Handled = e.SuppressKeyPress = true; }
        else if (e.KeyCode == Keys.Right && _boxes[idx].SelectionStart == _boxes[idx].TextLength && idx < 2) { _boxes[idx + 1].Focus(); e.Handled = true; }
        else if (e.KeyCode == Keys.Left && _boxes[idx].SelectionStart == 0 && idx > 0) { _boxes[idx - 1].Focus(); e.Handled = true; }
    }

    Rectangle Cell(int i)
    {
        int unitW = S(34), gap = S(8);
        int cw = (Width - 2 * gap - 3 * unitW) / 3;
        return new Rectangle(i * (cw + unitW + gap), 0, cw, Height);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (_boxes[2] == null) return; // kurucu kutuları eklerken
        for (int i = 0; i < 3; i++)
        {
            var c = Cell(i);
            int h = _boxes[i].PreferredHeight;
            _boxes[i].SetBounds(c.X + S(8), c.Y + (c.Height - h) / 2, c.Width - S(16), h);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        for (int i = 0; i < 3; i++) if (Cell(i).Contains(e.Location)) { _boxes[i].Focus(); break; }
        base.OnMouseDown(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        foreach (var b in _boxes) { b.Enabled = Enabled; b.ForeColor = Enabled ? P.Text : P.SubText; }
        base.OnEnabledChanged(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = Begin(e);
        for (int i = 0; i < 3; i++)
        {
            var c = Cell(i);
            Draw.Fill(g, c, Sf(12), Dim(P.Neutral));
            bool focus = _boxes[i].Focused;
            Draw.Stroke(g, RectangleF.Inflate(c, -0.75f, -0.75f), Sf(12), focus ? P.Accent : Dim(P.Border), focus ? Sf(2) : 1);
            DrawText(g, _units[i], _unitFont, new Rectangle(c.Right + S(6), 0, S(34), Height), Dim(P.SubText), TextFormatFlags.VerticalCenter);
        }
    }

    protected override void Dispose(bool disposing) { if (disposing) _debounce.Dispose(); base.Dispose(disposing); }
}
