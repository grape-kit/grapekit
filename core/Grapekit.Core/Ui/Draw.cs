using System.Drawing.Drawing2D;

namespace Grapekit.Ui;

/// <summary>Ortak çizim yardımcıları: yuvarlatılmış dikdörtgen, dolgu, kenarlık, ortalanmış glif.</summary>
public static class Draw
{
    public const string IconFont = "Segoe MDL2 Assets";

    public static GraphicsPath Round(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0.5f) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static void Fill(Graphics g, RectangleF r, float radius, Color c)
    {
        using var path = Round(r, radius);
        using var b = new SolidBrush(c);
        g.FillPath(b, path);
    }

    public static void Stroke(Graphics g, RectangleF r, float radius, Color c, float width = 1f)
    {
        using var path = Round(r, radius);
        using var pen = new Pen(c, width);
        g.DrawPath(pen, path);
    }

    public static bool IsIcon(string s) => s.Length == 1 && s[0] >= '' && s[0] <= '';

    /// <summary>Harfi/simgeyi kutunun tam ortasına (gerçek glif sınırlarına göre) çizer.</summary>
    public static void Glyph(Graphics g, string text, RectangleF box, float em, Color color)
    {
        using var path = new GraphicsPath();
        FontFamily ff;
        try { ff = new FontFamily(IsIcon(text) ? IconFont : "Segoe UI"); }
        catch { ff = new FontFamily("Segoe UI"); }
        using (ff)
            path.AddString(text, ff, (int)(IsIcon(text) ? FontStyle.Regular : FontStyle.Bold), em, PointF.Empty, StringFormat.GenericTypographic);
        var b = path.GetBounds();
        if (b.Width <= 0) return;
        using var m = new Matrix();
        m.Translate(box.X + (box.Width - b.Width) / 2 - b.X, box.Y + (box.Height - b.Height) / 2 - b.Y);
        path.Transform(m);
        using var br = new SolidBrush(color);
        g.FillPath(br, path);
    }
}
