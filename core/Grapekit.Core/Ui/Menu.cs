namespace Grapekit.Ui;

/// <summary>Sağ tık menüsünü açık/koyu temaya uydurur.</summary>
public sealed class MenuRenderer(Palette p) : ToolStripProfessionalRenderer(new MenuColors(p))
{
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.ForeColor == p.AccentText || e.Item.ForeColor == p.Warning ? e.Item.ForeColor : p.Text;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = p.Text;
        base.OnRenderArrow(e);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var r = e.ImageRectangle;
        using var pen = new Pen(p.AccentText, Math.Max(1.6f, r.Height / 8f))
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round,
            LineJoin = System.Drawing.Drawing2D.LineJoin.Round,
        };
        g.DrawLines(pen, [
            new PointF(r.X + r.Width * 0.18f, r.Y + r.Height * 0.52f),
            new PointF(r.X + r.Width * 0.42f, r.Y + r.Height * 0.74f),
            new PointF(r.X + r.Width * 0.84f, r.Y + r.Height * 0.28f),
        ]);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        int y = e.Item.Height / 2;
        using var pen = new Pen(p.Border);
        e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
    }
}

public sealed class MenuColors(Palette p) : ProfessionalColorTable
{
    Color Sel => p.HighContrast ? p.Accent : p.Dark ? ThemeInfo.C(0x3D3D3D) : ThemeInfo.C(0xEBEBEB);
    public override Color ToolStripDropDownBackground => p.Card;
    public override Color ImageMarginGradientBegin => p.Card;
    public override Color ImageMarginGradientMiddle => p.Card;
    public override Color ImageMarginGradientEnd => p.Card;
    public override Color MenuBorder => p.Border;
    public override Color MenuItemBorder => Sel;
    public override Color MenuItemSelected => Sel;
    public override Color MenuItemSelectedGradientBegin => Sel;
    public override Color MenuItemSelectedGradientEnd => Sel;
    public override Color MenuItemPressedGradientBegin => Sel;
    public override Color MenuItemPressedGradientEnd => Sel;
    public override Color CheckBackground => p.Card;
    public override Color CheckSelectedBackground => Sel;
    public override Color CheckPressedBackground => Sel;
    public override Color SeparatorDark => p.Border;
    public override Color SeparatorLight => p.Card;
}

/// <summary>Kısayol tuşu birleşimini okunur metne çevirir (ör. "Ctrl + Alt + C"). combo = (değiştiriciler &lt;&lt; 16) | tuş</summary>
public static class HotkeyText
{
    public static string Describe(int combo)
    {
        if ((combo & 0xFFFF) == 0) return "—";
        uint mods = (uint)(combo >> 16);
        var parts = new List<string>();
        if ((mods & Win32.MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((mods & Win32.MOD_ALT) != 0) parts.Add("Alt");
        if ((mods & Win32.MOD_SHIFT) != 0) parts.Add("Shift");
        if ((mods & Win32.MOD_WIN) != 0) parts.Add("Win");
        var key = (Keys)(combo & 0xFFFF);
        parts.Add(key switch
        {
            >= Keys.D0 and <= Keys.D9 => ((char)('0' + (key - Keys.D0))).ToString(),
            Keys.Oemcomma => ",",
            Keys.OemPeriod => ".",
            Keys.OemMinus => "-",
            Keys.Oemplus => "+",
            _ => key.ToString(),
        });
        return string.Join(" + ", parts);
    }
}
