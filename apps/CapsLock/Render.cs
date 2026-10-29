using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using static Grapekit.Ui.Draw;

namespace CapsGosterge;

/// <summary>Ekran kartının içeriği.</summary>
/// <param name="Glyph">Tuş üzerindeki harf/simge. U+E000–U+F8FF aralığındakiler Segoe MDL2 Assets ile çizilir.</param>
/// <param name="Style">0 = nötr, 1 = vurgulu (açık), 2 = uyarı</param>
internal sealed record CardSpec(string Glyph, int Style, string Title, string Sub);

internal static class Render
{
    static (Color face, Color fg) KeyColors(int style, Palette p) => style switch
    {
        1 => (p.Accent, p.OnAccent),
        2 => (p.Warning, ThemeInfo.Luminance(p.Warning) > 0.5 ? Color.FromArgb(25, 25, 25) : Color.White),
        _ => (p.HighContrast ? p.Card : p.Dark ? ThemeInfo.C(0x454545) : ThemeInfo.C(0xE4E4E4), p.Text),
    };

    /// <summary>Klavye tuşu görünümlü simge.</summary>
    public static void Keycap(Graphics g, RectangleF r, string glyph, int style, Palette p, bool shade = true)
    {
        float rad = r.Width * 0.22f;
        var (face, fg) = KeyColors(style, p);
        var top = r;
        if (shade && !p.HighContrast)
        {
            Fill(g, r, rad, ThemeInfo.Mix(face, Color.Black, p.Dark ? 0.40 : 0.16));
            top = new RectangleF(r.X, r.Y, r.Width, r.Height - r.Height * 0.08f);
        }
        Fill(g, top, rad, face);
        if (p.HighContrast) Stroke(g, top, rad, p.Text, 1.5f);
        float em = IsIcon(glyph) ? r.Height * 0.46f : glyph.Length > 1 ? r.Height * 0.38f : r.Height * 0.62f;
        Glyph(g, glyph, top, em, fg);
    }

    public static void Keycap(Graphics g, RectangleF r, bool on, Palette p, bool shade = true) =>
        Keycap(g, r, on ? "A" : "a", on ? 1 : 0, p, shade);

    static void Shadow(Graphics g, RectangleF r, float radius, int spread, bool dark)
    {
        if (spread <= 0) return;
        int target = dark ? 110 : 60;
        int a = Math.Max(1, target / spread);
        float yOff = spread * 0.2f;
        using var b = new SolidBrush(Color.FromArgb(a, 0, 0, 0));
        for (int i = spread; i >= 1; i--)
        {
            var rr = RectangleF.Inflate(r, i, i);
            rr.Offset(0, yOff);
            using var path = Round(rr, radius + i);
            g.FillPath(b, path);
        }
    }

    static Graphics Prepare(Bitmap bmp)
    {
        var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        return g;
    }

    static SizeF Measure(string text, Font f)
    {
        using var tmp = new Bitmap(1, 1);
        using var g = Graphics.FromImage(tmp);
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        return g.MeasureString(text, f, int.MaxValue, StringFormat.GenericTypographic);
    }

    /// <summary>Ekran bildirimi kartı. margin = gölge payı. arrowUp: üstte, alana işaret eden ok (şifre uyarısı).</summary>
    public static Bitmap Card(CardSpec c, Palette p, float s, out int margin, bool arrowUp = false)
    {
        int m = (int)Math.Round(22 * s);
        margin = m;

        using var fT = new Font("Segoe UI", 24 * s, FontStyle.Bold, GraphicsUnit.Pixel);
        using var fS = new Font("Segoe UI", 14 * s, FontStyle.Regular, GraphicsUnit.Pixel);
        var tSize = Measure(c.Title, fT);
        var sSize = Measure(c.Sub, fS);

        float key = 64 * s, pad = 18 * s, dot = 8 * s, arrow = arrowUp ? 10 * s : 0;
        float textW = Math.Max(tSize.Width, sSize.Width + dot + 8 * s);
        int w = (int)Math.Ceiling(pad + key + pad + textW + pad * 1.4f);
        int h = (int)Math.Round(100 * s);

        var bmp = new Bitmap(w + 2 * m, h + 2 * m + (int)arrow, PixelFormat.Format32bppArgb);
        using var g = Prepare(bmp);
        var card = new RectangleF(m, m + arrow, w, h);
        float rad = 16 * s;
        Color strip = c.Style == 2 ? p.Warning : p.Accent;

        Shadow(g, card, rad, m - (int)(4 * s), p.Dark);
        using (var path = Round(card, rad))
        {
            if (arrowUp)
            {
                float ax = card.X + 28 * s;
                path.AddPolygon([new PointF(ax - arrow, card.Y + 1), new PointF(ax, card.Y - arrow), new PointF(ax + arrow, card.Y + 1)]);
            }
            using var b = new SolidBrush(p.Card);
            g.FillPath(b, path);
        }
        Stroke(g, RectangleF.Inflate(card, -0.5f, -0.5f), rad, c.Style == 2 ? p.Warning : p.Border, Math.Max(1f, c.Style == 2 ? 2 * s : s));

        if (c.Style != 0)
        {
            using var clip = Round(card, rad);
            g.SetClip(clip);
            using (var b = new SolidBrush(strip)) g.FillRectangle(b, card.X, card.Y, 5 * s, card.Height);
            g.ResetClip();
        }

        var keyRect = new RectangleF(card.X + pad, card.Y + (h - key) / 2, key, key);
        Keycap(g, keyRect, c.Glyph, c.Style, p);

        float tx = keyRect.Right + pad;
        float th = tSize.Height, sh = sSize.Height, gap = 4 * s;
        float ty = card.Y + (h - (th + gap + sh)) / 2;
        Color titleColor = c.Style switch { 1 => p.AccentText, 2 => p.Warning, _ => p.Text };
        using (var b = new SolidBrush(titleColor))
            g.DrawString(c.Title, fT, b, tx, ty, StringFormat.GenericTypographic);

        float sy = ty + th + gap;
        using (var b = new SolidBrush(c.Style == 0 ? p.SubText : strip))
            g.FillEllipse(b, tx, sy + (sh - dot) / 2, dot, dot);
        using (var b = new SolidBrush(p.SubText))
            g.DrawString(c.Sub, fS, b, tx + dot + 8 * s, sy, StringFormat.GenericTypographic);

        return bmp;
    }

    /// <summary>Büyük harf açıkken ekranda kalan küçük rozet.</summary>
    public static Bitmap Badge(Palette p, float s, out int margin)
    {
        int m = (int)Math.Round(12 * s);
        margin = m;
        string text = L.T("badge");
        using var f = new Font("Segoe UI Semibold", 13 * s, FontStyle.Regular, GraphicsUnit.Pixel);
        var ts = Measure(text, f);

        float h = 36 * s, key = 24 * s, pad = 6 * s;
        int w = (int)Math.Ceiling(pad + key + 10 * s + ts.Width + 16 * s);
        var bmp = new Bitmap(w + 2 * m, (int)Math.Ceiling(h) + 2 * m, PixelFormat.Format32bppArgb);
        using var g = Prepare(bmp);
        var card = new RectangleF(m, m, w, h);

        Shadow(g, card, h / 2, m - (int)(3 * s), p.Dark);
        Fill(g, card, h / 2, p.Card);
        Stroke(g, RectangleF.Inflate(card, -0.5f, -0.5f), h / 2, p.Accent, Math.Max(1.5f, 1.5f * s));

        var keyRect = new RectangleF(card.X + pad, card.Y + (h - key) / 2, key, key);
        Fill(g, keyRect, key / 2, p.Accent);
        Glyph(g, "A", keyRect, key * 0.62f, p.OnAccent);

        using var b = new SolidBrush(p.Text);
        g.DrawString(text, f, b, keyRect.Right + 10 * s, card.Y + (h - ts.Height) / 2, StringFormat.GenericTypographic);
        return bmp;
    }

    /// <summary>Bildirim alanı simgesi. Döndürülen HICON çağıran tarafından yok edilmelidir.</summary>
    public static IntPtr TrayIcon(bool on, bool paused, Palette p, bool taskbarLight, bool taskbarAccent, int size)
    {
        using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Prepare(bmp);
        var r = new RectangleF(0.5f, 0.5f, size - 1f, size - 1f);
        float rad = size * 0.24f;
        Color fg = taskbarLight ? ThemeInfo.C(0x1C1C1C) : Color.White;

        if (on)
        {
            Color face = taskbarAccent ? fg : p.Accent;
            Color glyph = taskbarAccent ? p.Accent : p.OnAccent;
            Fill(g, r, rad, face);
            Glyph(g, "A", r, size * 0.86f, glyph);
        }
        else
        {
            float pw = Math.Max(1.2f, size / 14f);
            Stroke(g, RectangleF.Inflate(r, -pw / 2, -pw / 2), rad, Color.FromArgb(200, fg), pw);
            Glyph(g, "a", r, size * 0.86f, fg);
        }

        if (paused)
        {
            // Sağ altta küçük "duraklatıldı" işareti
            float d = size * 0.5f;
            var c = new RectangleF(size - d, size - d, d, d);
            using (var b = new SolidBrush(taskbarLight ? Color.White : ThemeInfo.C(0x202020))) g.FillEllipse(b, RectangleF.Inflate(c, 1, 1));
            using (var b = new SolidBrush(p.Warning)) g.FillEllipse(b, c);
            float bw = d * 0.16f, bh = d * 0.5f;
            using var bb = new SolidBrush(Color.FromArgb(30, 30, 30));
            g.FillRectangle(bb, c.X + d * 0.3f, c.Y + d * 0.25f, bw, bh);
            g.FillRectangle(bb, c.X + d * 0.54f, c.Y + d * 0.25f, bw, bh);
        }
        return bmp.GetHicon();
    }

    /// <summary>Pencere simgesi.</summary>
    public static Icon AppIcon(Palette p, int size = 32)
    {
        using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Prepare(bmp))
            Keycap(g, new RectangleF(1, 1, size - 2, size - 2), true, p);
        IntPtr h = bmp.GetHicon();
        using var tmp = Icon.FromHandle(h);
        var icon = (Icon)tmp.Clone();
        Native.DestroyIcon(h);
        return icon;
    }
}
