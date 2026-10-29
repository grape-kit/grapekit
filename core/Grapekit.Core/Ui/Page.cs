using System.Drawing.Drawing2D;

namespace Grapekit.Ui;

/// <summary>
/// Kaydırılabilir ayar sayfası. Kartlar, başlıklar ve etiketler doğrudan çizilir;
/// etkileşimli öğeler gerçek kontrollerdir. Koordinatlar içerik uzayındadır (kaydırma ofseti çizimde eklenir).
/// </summary>
public sealed class Page : Panel
{
    public readonly Palette P;
    readonly Fonts _f;

    public readonly List<Rectangle> Cards = [];
    public readonly List<(string Text, Point At, bool Big)> Headings = [];
    public readonly List<(string Title, string? Desc, Rectangle Rect)> Labels = [];
    public readonly List<(int X1, int X2, int Y)> Dividers = [];
    public readonly List<(string Text, Rectangle Rect, Color Color)> Paragraphs = [];
    public readonly List<Action<Graphics, Point>> Painters = [];
    /// <summary>Kart köşe yarıçapı (mantıksal piksel).</summary>
    public int CardRadius = 8;

    public Page(Palette p, Fonts f)
    {
        P = p; _f = f;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        AutoScroll = true;
        BackColor = p.Bg;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // Koyu temada koyu kaydırma çubukları (Windows 10 1809+)
        try { SetWindowTheme(Handle, P.Dark ? "DarkMode_Explorer" : "Explorer", null); } catch { }
    }

    [System.Runtime.InteropServices.DllImport("uxtheme.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    static extern int SetWindowTheme(IntPtr hwnd, string? app, string? idList);

    protected override void OnScroll(ScrollEventArgs se) { base.OnScroll(se); Invalidate(); }
    protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); Invalidate(); }

    int S(int v) => LogicalToDeviceUnits(v);

    public void Clear()
    {
        foreach (var c in Controls.Cast<Control>().ToArray()) c.Dispose();
        Cards.Clear(); Headings.Clear(); Labels.Clear(); Dividers.Clear(); Paragraphs.Clear(); Painters.Clear();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        var o = AutoScrollPosition;
        Rectangle Off(Rectangle r) => new(r.X + o.X, r.Y + o.Y, r.Width, r.Height);

        foreach (var c in Cards)
        {
            var r = Off(c);
            Draw.Fill(g, r, S(CardRadius), P.Card);
            Draw.Stroke(g, new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1), S(CardRadius), P.Border);
        }

        foreach (var (text, at, big) in Headings)
            TextRenderer.DrawText(g, text, big ? _f.PageTitle : _f.Heading, new Point(at.X + o.X, at.Y + o.Y), P.Text, TextFormatFlags.NoPrefix);

        using (var pen = new Pen(P.Border))
            foreach (var (x1, x2, y) in Dividers) g.DrawLine(pen, x1 + o.X, y + o.Y, x2 + o.X, y + o.Y);

        const TextFormatFlags one = TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine;
        foreach (var (title, desc, rect) in Labels)
        {
            var r = Off(rect);
            var lines = desc?.Split('\n') ?? [];
            int th = _f.Row.Height, dh = _f.Desc.Height * lines.Length, gap = lines.Length > 0 ? S(2) : 0;
            int top = r.Y + (r.Height - (th + gap + dh)) / 2;
            TextRenderer.DrawText(g, title, _f.Row, new Rectangle(r.X, top, r.Width, th), P.Text, one);
            for (int i = 0; i < lines.Length; i++)
                TextRenderer.DrawText(g, lines[i], _f.Desc, new Rectangle(r.X, top + th + gap + i * _f.Desc.Height, r.Width, _f.Desc.Height), P.SubText, one);
        }

        foreach (var (text, rect, color) in Paragraphs)
            TextRenderer.DrawText(g, text, _f.Desc, Off(rect), color, TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);

        foreach (var p in Painters) p(g, o);
    }
}

public sealed class Fonts : IDisposable
{
    public Font PageTitle = null!, Heading = null!, Row = null!, Desc = null!, Ctl = null!, Nav = null!, Big = null!, Small = null!;
    readonly List<Font> _all = [];

    Font Keep(Font f) { _all.Add(f); return f; }

    /// <summary>Grapekit marka yazı tipleri: başlıklar Bricolage Grotesque, metin Figtree.</summary>
    public void CreateBrand(int dpi)
    {
        Dispose();
        float k = dpi / 96f;
        PageTitle = Keep(BrandFonts.Display(24 * k));
        Heading = Keep(BrandFonts.Display(17 * k));
        Row = Keep(BrandFonts.BodySemi(14 * k));
        Desc = Keep(BrandFonts.Body(13 * k));
        Ctl = Keep(BrandFonts.BodySemi(13 * k));
        Nav = Keep(BrandFonts.BodySemi(14 * k));
        Big = Keep(BrandFonts.Display(30 * k));
        Small = Keep(BrandFonts.Body(12 * k));
    }

    public void Create(int dpi)
    {
        Dispose();
        Font F(string family, float px) => Keep(new Font(family, px * dpi / 96f, FontStyle.Regular, GraphicsUnit.Pixel));
        PageTitle = F("Segoe UI Semibold", 26);
        Heading = F("Segoe UI Semibold", 14);
        Row = F("Segoe UI", 14);
        Desc = F("Segoe UI", 12);
        Ctl = F("Segoe UI", 13);
        Nav = F("Segoe UI", 14);
        Big = F("Segoe UI Semibold", 30);
        Small = F("Segoe UI", 11);
    }

    public void Dispose()
    {
        foreach (var f in _all) f.Dispose();
        _all.Clear();
    }
}
