using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace Grapekit;

/// <summary>Grapekit markasının sabitleri: adresler, renkler ve logo çizimi.</summary>
public static class Brand
{
    public const string Name = "Grapekit";
    public const string Website = "https://grapekit.com";
    public const string GitHub = "https://github.com/grape-kit/grapekit";
    public const string RawBase = "https://raw.githubusercontent.com/grape-kit/grapekit/main";
    public const string Support = "https://buymeacoffee.com/grapekit";
    public const string Copyright = "© 2026 Grapekit";
    public const string License = "MIT";

    public static string PrivacyUrl => GrapekitApp.Lang == 0 ? Website + "/gizlilik-politikasi" : Website + "/en/privacy-policy";
    public static string AllAppsUrl => GrapekitApp.Lang == 0 ? Website + "/uygulamalar" : Website + "/en/apps";

    // Marka renkleri
    public static readonly Color Grape = Color.FromArgb(0x7C, 0x3A, 0xED);
    public static readonly Color GrapeLight = Color.FromArgb(0xA7, 0x8B, 0xFA);
    public static readonly Color Lavender = Color.FromArgb(0xC4, 0xB5, 0xFD);
    public static readonly Color Night = Color.FromArgb(0x1A, 0x0B, 0x33);
    public static readonly Color Leaf = Color.FromArgb(0x22, 0xC5, 0x5E);
    public static readonly Color Coffee = Color.FromArgb(0xFF, 0xDD, 0x00);

    /// <summary>Adresi varsayılan tarayıcıda açar (yalnızca https).</summary>
    public static void Open(string url)
    {
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }

    static readonly (float X, float Y)[] Tiles = [(34, 54), (80, 54), (126, 54), (57, 98), (103, 98), (80, 142)];

    /// <summary>Grapekit işaretini (altı kutucuktan üzüm salkımı) verilen kareye çizer. mono: tek renk.</summary>
    public static void PaintMark(Graphics g, RectangleF box, Color? mono = null)
    {
        var state = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        float k = Math.Min(box.Width, box.Height) / 200f;
        g.TranslateTransform(box.X + (box.Width - 200 * k) / 2, box.Y + (box.Height - 200 * k) / 2);
        g.ScaleTransform(k, k);
        g.TranslateTransform(0, 6);   // içerik kutusu: y 6..182 → ortala

        using (var stem = new Pen(mono ?? Color.FromArgb(0x4D, 0x7C, 0x0F), 6) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawBezier(stem, 100, 56, 100, 42, 103, 30, 110, 18);
        using (var leaf = new GraphicsPath())
        {
            leaf.AddBezier(104, 34, 112, 14, 140, 6, 162, 14);
            leaf.AddBezier(162, 14, 152, 36, 126, 46, 104, 34);
            using Brush lb = mono is { } mc ? new SolidBrush(mc) : new LinearGradientBrush(new PointF(104, 46), new PointF(162, 6), Color.FromArgb(0x15, 0x80, 0x3D), Color.FromArgb(0xBB, 0xF7, 0xD0));
            g.FillPath(lb, leaf);
        }
        for (int i = 0; i < Tiles.Length; i++)
        {
            var (x, y) = Tiles[i];
            var r = new RectangleF(x, y, 40, 40);
            using var path = RoundRect(r, 14);
            using Brush b = mono is { } mc ? new SolidBrush(mc)
                : new LinearGradientBrush(new PointF(x, y), new PointF(x + 40, y + 40), i % 2 == 0 ? Color.FromArgb(0xD8, 0xCC, 0xFF) : Color.FromArgb(0xB9, 0xA3, 0xFF), i % 2 == 0 ? Color.FromArgb(0x5B, 0x21, 0xB6) : Color.FromArgb(0x4C, 0x1D, 0x95));
            g.FillPath(b, path);
            if (mono == null)
            {
                using var gl = RoundRect(new RectangleF(x + 7, y + 6, 12, 6), 3);
                using var wb = new SolidBrush(Color.FromArgb(158, 255, 255, 255));
                g.FillPath(wb, gl);
            }
        }
        g.Restore(state);
    }

    static GraphicsPath RoundRect(RectangleF r, float rad)
    {
        var p = new GraphicsPath();
        float d = rad * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
