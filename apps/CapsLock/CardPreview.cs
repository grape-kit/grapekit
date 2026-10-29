using System.Drawing.Drawing2D;

namespace CapsGosterge;

internal sealed class CardPreview : ThemedControl
{
    Bitmap? _bmp;
    int _opacity = 100;

    public CardPreview(Palette p) : base(p) { TabStop = false; Cursor = Cursors.Default; }

    public void SetPreview(Bitmap? bmp, int opacity)
    {
        _bmp?.Dispose();
        _bmp = bmp;
        _opacity = opacity;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = Begin(e);
        var r = new RectangleF(0, 0, Width, Height);
        using (var path = Draw.Round(r, Sf(8)))
        using (var lg = new LinearGradientBrush(r, ThemeInfo.C(0x3A6EA5), ThemeInfo.C(0x8E5BB5), 30f))
            g.FillPath(lg, path);

        // Zemin üzerine birkaç "pencere" çiz ki saydamlık görülebilsin
        using (var b = new SolidBrush(Color.FromArgb(70, 255, 255, 255)))
        {
            g.FillRectangle(b, Width * 0.08f, Height * 0.15f, Width * 0.35f, Height * 0.5f);
            g.FillRectangle(b, Width * 0.55f, Height * 0.3f, Width * 0.38f, Height * 0.55f);
        }

        if (_bmp == null) return;
        float k = Math.Min(1f, Math.Min((Width - Sf(16)) / _bmp.Width, (Height - Sf(16)) / _bmp.Height));
        float w = _bmp.Width * k, h = _bmp.Height * k;
        var dst = new RectangleF((Width - w) / 2, (Height - h) / 2, w, h);
        using var attrs = new System.Drawing.Imaging.ImageAttributes();
        var cm = new System.Drawing.Imaging.ColorMatrix { Matrix33 = _opacity / 100f };
        attrs.SetColorMatrix(cm);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(_bmp, Rectangle.Round(dst), 0, 0, _bmp.Width, _bmp.Height, GraphicsUnit.Pixel, attrs);
    }

    protected override void Dispose(bool disposing) { if (disposing) _bmp?.Dispose(); base.Dispose(disposing); }
}

/// <summary>Basit çubuk grafik: iki seri (ör. açma sayısı ve yanlışlıkla basma).</summary>
