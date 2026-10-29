using System.Drawing.Drawing2D;

namespace Grapekit.Ui;

/// <summary>
/// Tepsi simgesine tıklayınca saatin üstünde açılan Grapekit paneli.
/// Üstte gece moru bant (simge, ad, durum rozeti + uygulamanın "hero" kontrolü), altta beyaz gövde
/// (uygulamanın kontrolleri), en altta Grapekit işareti, kısayol ipucu ve Ayarlar.
/// Odak kaybolunca kendiliğinden kapanır.
/// </summary>
public class FlyoutWindow : Form
{
    sealed class Entry(Control? control, string? label, int height, int gap)
    {
        public Control? Control = control; public string? Label = label; public int Height = height, Gap = gap, Y; public bool Hidden;
    }

    protected Kit K { get; }
    protected Palette P => K.P;
    readonly List<Entry> _entries = [];
    Control? _hero;
    int _heroH;
    readonly PillButton _settings;
    Font? _fName, _fPill, _fLabel, _fBrand, _fHint;
    int _footerY;
    string _status = "", _hint = "";
    bool _statusOn;
    Point _anchor;

    const int LogicalWidth = 380;

    public DateTime HiddenAt { get; private set; }
    public event Action? SettingsRequested;

    public FlyoutWindow(Kit k)
    {
        K = k;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        KeyPreview = true;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = k.P.Bg;
        Text = GrapekitApp.Name;
        DoubleBuffered = true;
        _settings = new PillButton(k.P, CoreText.T("settings"), Font, k.Foot, k.Foot, k.P.AccentText) { Icon = "" };
        _settings.Click += (_, _) => { Hide(); SettingsRequested?.Invoke(); };
        Controls.Add(_settings);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ClassStyle |= 0x20000;  // CS_DROPSHADOW
            cp.ExStyle |= 0x80;        // WS_EX_TOOLWINDOW: Alt+Tab'da görünmez
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int round = 2; // Windows 11: DWMWCP_ROUND
        Win32.DwmSetWindowAttribute(Handle, 33, ref round, 4);
    }

    public string StatusText { get => _status; set { if (_status == value) return; _status = value; Invalidate(new Rectangle(0, 0, Width, S(64))); } }
    public bool StatusOn { get => _statusOn; set { if (_statusOn == value) return; _statusOn = value; Invalidate(new Rectangle(0, 0, Width, S(64))); } }
    /// <summary>Alt bilgideki kısayol ipucu (ör. "Ctrl+Alt+U"); boşsa gösterilmez.</summary>
    public string Hint { get => _hint; set { if (_hint == value) return; _hint = value; if (IsHandleCreated) Relayout(); } }

    protected int S(int v) => (int)Math.Round(v * DeviceDpi / 96.0);
    protected float Sf(float v) => v * DeviceDpi / 96f;

    /// <summary>Mor bandın içindeki asıl kontrol (ör. büyük aç/kapa halkası). Yüzeyi K.Hero olmalı.</summary>
    protected void SetHero(Control c, int logicalHeight)
    {
        _hero = c; _heroH = logicalHeight;
        Controls.Add(c);
    }

    protected void AddLabel(string text, int gap = 0) => _entries.Add(new Entry(null, text, 22, gap));

    protected void AddBody(Control c, int logicalHeight, int gap = 0)
    {
        _entries.Add(new Entry(c, null, logicalHeight, gap));
        Controls.Add(c);
    }

    /// <summary>Gövdedeki bir kontrolü gösterir/gizler; panel yüksekliği buna göre değişir.</summary>
    protected void SetBodyVisible(Control c, bool visible)
    {
        var e = _entries.FirstOrDefault(x => x.Control == c);
        if (e == null || e.Hidden == !visible) return;
        e.Hidden = !visible;
        if (!IsHandleCreated) return;
        Relayout();
        if (Visible) Place();
    }

    int HeaderH => S(18 + 30 + 10);
    int BandH => HeaderH + S(_heroH) + S(20);

    void MakeFonts()
    {
        foreach (var f in new[] { _fName, _fPill, _fLabel, _fBrand, _fHint }) f?.Dispose();
        float k = DeviceDpi / 96f;
        _fName = BrandFonts.Display(16 * k);
        _fPill = BrandFonts.BodySemi(12 * k);
        _fLabel = BrandFonts.BodySemi(12 * k);
        _fBrand = BrandFonts.Display(14 * k);
        _fHint = BrandFonts.BodySemi(12 * k);
        _settings.Font = BrandFonts.BodySemi(13 * k);
    }

    protected void Relayout()
    {
        MakeFonts();
        int w = S(LogicalWidth);
        if (_hero != null) _hero.Bounds = new Rectangle(0, HeaderH, w, S(_heroH));
        int y = BandH + S(16);
        foreach (var e in _entries)
        {
            if (e.Control != null) e.Control.Visible = !e.Hidden;
            if (e.Hidden) continue;
            y += S(e.Gap);
            e.Y = y;
            e.Control?.SetBounds(S(20), y, w - S(40), S(e.Height));
            y += S(e.Height);
        }
        _footerY = y + S(12);
        int bw = _settings.PreferredWidth;
        _settings.SetBounds(w - S(12) - bw, _footerY + S(8), bw, S(36));
        ClientSize = new Size(w, _footerY + S(52));
        Invalidate();
    }

    /// <summary>Paneli tepsi alanının yanında açar (görev çubuğu hangi kenardaysa oraya).</summary>
    public void ShowNearTray()
    {
        _anchor = Cursor.Position;
        if (!IsHandleCreated) CreateHandle();
        Relayout();
        Place();
        Show();
        Activate();
    }

    void Place()
    {
        var scr = Screen.FromPoint(_anchor);
        Rectangle wa = scr.WorkingArea, b = scr.Bounds;
        int m = S(12), x, y;
        if (wa.Left > b.Left) x = wa.Left + m;
        else if (wa.Right < b.Right) x = wa.Right - Width - m;
        else x = Math.Clamp(_anchor.X - Width / 2, wa.Left + m, Math.Max(wa.Left + m, wa.Right - Width - m));
        y = wa.Top > b.Top ? wa.Top + m : wa.Bottom - Height - m;
        if (wa.Left > b.Left || wa.Right < b.Right) y = Math.Clamp(_anchor.Y - Height / 2, wa.Top + m, Math.Max(wa.Top + m, wa.Bottom - Height - m));
        Location = new Point(x, y);
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        Relayout();
        Place();
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        HiddenAt = DateTime.UtcNow;
        Hide();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape) { Hide(); e.Handled = true; }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (_fName == null) return;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(P.Bg);
        using (var hb = new SolidBrush(K.Hero)) g.FillRectangle(hb, 0, 0, Width, BandH);

        // Başlık satırı: simge, uygulama adı, durum rozeti
        int top = S(18), rowH = S(30);
        AppIcon.Paint(g, new Rectangle(S(20), top, rowH, rowH));
        var pillText = _status;
        int pw = pillText.Length > 0 ? TextRenderer.MeasureText(pillText, _fPill).Width + S(20) : 0;
        int nameX = S(20) + rowH + S(10);
        TextRenderer.DrawText(g, GrapekitApp.Name, _fName, new Rectangle(nameX, top, Width - nameX - pw - S(28), rowH), K.HeroText,
            TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        if (pw > 0)
        {
            var pr = new Rectangle(Width - S(20) - pw, top + (rowH - S(24)) / 2, pw, S(24));
            Color pbg = _statusOn ? K.Ring : ThemeInfo.Mix(K.Hero, Color.White, 0.14), pfg = _statusOn ? Brand.Night : K.HeroText;
            Draw.Fill(g, pr, pr.Height / 2f, pbg);
            TextRenderer.DrawText(g, pillText, _fPill, pr, pfg, TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }

        foreach (var en in _entries)
            if (en.Label != null && !en.Hidden)
                TextRenderer.DrawText(g, en.Label.ToUpper(CoreText.Culture), _fLabel!, new Rectangle(S(20), en.Y, Width - S(40), S(en.Height)), P.SubText,
                    TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

        // Alt bilgi: Grapekit işareti ve adı, kısayol ipucu
        using (var fb = new SolidBrush(K.Foot)) g.FillRectangle(fb, 0, _footerY, Width, Height - _footerY);
        using (var pen = new Pen(P.Border)) g.DrawLine(pen, 0, _footerY, Width, _footerY);
        int fy = _footerY, fh = S(52);
        Brand.PaintMark(g, new RectangleF(S(18), fy + (fh - S(20)) / 2f, S(20), S(20)), K.Dark ? K.Ring : Brand.Grape);
        TextRenderer.DrawText(g, "grapekit", _fBrand!, new Rectangle(S(44), fy, S(120), fh), P.Text,
            TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        if (_hint.Length > 0)
        {
            int hw = TextRenderer.MeasureText(_hint, _fHint).Width + S(14);
            var hr = new Rectangle(_settings.Left - S(8) - hw, fy + (fh - S(24)) / 2, hw, S(24));
            Draw.Stroke(g, new RectangleF(hr.X + 0.5f, hr.Y + 0.5f, hr.Width - 1, hr.Height - 1), Sf(6), P.Border);
            TextRenderer.DrawText(g, _hint, _fHint!, hr, P.SubText, TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
        using (var pen = new Pen(K.Dark ? P.Border : ThemeInfo.Mix(P.Border, Brand.Night, 0.15)))
            g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) foreach (var f in new[] { _fName, _fPill, _fLabel, _fBrand, _fHint }) f?.Dispose();
        base.Dispose(disposing);
    }
}
