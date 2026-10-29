using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace Grapekit.Ui;

/// <summary>
/// Grapekit arayüz renkleri. P: kontrollerin kullandığı palet; geri kalanı gece moru bant (Hero) ve
/// marka yüzeyleri için. Tüm Grapekit pencereleri (tepsi paneli, ayarlar) buradan beslenir.
/// </summary>
public sealed record Kit(Palette P, Color Hero, Color HeroText, Color HeroSub, Color HeroHover, Color Ring, Color Foot, Color AccentSoft)
{
    public bool Dark => P.Dark;

    /// <param name="themeSetting">0 = sistem, 1 = açık, 2 = koyu</param>
    public static Kit Get(int themeSetting)
    {
        var C = ThemeInfo.C;
        if (SystemInformation.HighContrast)
        {
            var p = ThemeInfo.Get(0, 0);
            return new Kit(p, p.Bg, p.Text, p.Text, p.Hover, p.Accent, p.Bg, p.Bg);
        }
        bool dark = themeSetting switch { 1 => false, 2 => true, _ => !ThemeInfo.AppsUseLight() };
        return dark
            ? new Kit(new Palette(true,
                    Bg: C(0x160A2C), Card: C(0x1E1038), Border: C(0x2E1D52),
                    Text: C(0xF1ECFF), SubText: C(0xB4A9CF),
                    Accent: Brand.Grape, OnAccent: Color.White, AccentText: C(0xC4B5FD),
                    Neutral: C(0x241544), Hover: C(0x1E1038), Warning: C(0xFCB93F)),
                Hero: C(0x2A1260), HeroText: Color.White, HeroSub: C(0xCFC3F5), HeroHover: C(0x3A1D7A),
                Ring: Brand.GrapeLight, Foot: C(0x120824), AccentSoft: C(0x2A1A52))
            : new Kit(new Palette(false,
                    Bg: Color.White, Card: C(0xF7F4FD), Border: C(0xE7E1F3),
                    Text: Brand.Night, SubText: C(0x5E5475),
                    Accent: Brand.Grape, OnAccent: Color.White, AccentText: C(0x6D28D9),
                    Neutral: C(0xF3EFFB), Hover: C(0xF8F5FE), Warning: C(0x9D5D00)),
                Hero: Brand.Night, HeroText: Color.White, HeroSub: C(0xCFC3F5), HeroHover: C(0x2E1A55),
                Ring: Brand.GrapeLight, Foot: C(0xFAF8FE), AccentSoft: C(0xF1EAFE));
    }
}

/// <summary>
/// Marka yazı tipleri (uygulamanın içinde gömülü, SIL OFL): başlık ve sayılar için Bricolage Grotesque,
/// metin için Figtree. Yüklenemezse Segoe UI kullanılır.
/// </summary>
public static class BrandFonts
{
    static PrivateFontCollection? _pfc;
    static FontFamily? _display, _body, _bodySemi;

    [DllImport("gdi32.dll")]
    static extern IntPtr AddFontMemResourceEx(IntPtr pbFont, uint cbFont, IntPtr pdv, out uint pcFonts);

    static void Load()
    {
        if (_pfc != null) return;
        _pfc = new PrivateFontCollection();
        var asm = typeof(BrandFonts).Assembly;
        foreach (var name in new[] { "Bricolage-Bold", "Figtree-Regular", "Figtree-SemiBold" })
        {
            try
            {
                using var s = asm.GetManifestResourceStream($"Grapekit.Fonts.{name}.ttf");
                if (s == null) continue;
                var data = new byte[s.Length];
                s.ReadExactly(data);
                // Bellek program boyunca ayrılı kalır: hem GDI+ (DrawString) hem GDI (TextRenderer) kullanır.
                var mem = Marshal.AllocCoTaskMem(data.Length);
                Marshal.Copy(data, 0, mem, data.Length);
                _pfc.AddMemoryFont(mem, data.Length);
                AddFontMemResourceEx(mem, (uint)data.Length, IntPtr.Zero, out _);
            }
            catch { }
        }
        FontFamily? Find(string n) => _pfc!.Families.FirstOrDefault(f => f.Name == n);
        _display = Find("Bricolage Grotesque 24pt");
        _body = Find("Figtree");
        _bodySemi = Find("Figtree SemiBold");
    }

    /// <summary>Başlık / büyük sayı yazı tipi. px: piksel boyutu (DPI ile çarpılmış).</summary>
    public static Font Display(float px) { Load(); return Make(_display, FontStyle.Bold, "Segoe UI Semibold", px); }
    public static Font Body(float px) { Load(); return Make(_body, FontStyle.Regular, "Segoe UI", px); }
    public static Font BodySemi(float px) { Load(); return Make(_bodySemi, FontStyle.Regular, "Segoe UI Semibold", px); }

    static Font Make(FontFamily? ff, FontStyle style, string fallback, float px)
    {
        try
        {
            if (ff != null && ff.IsStyleAvailable(style)) return new Font(ff, px, style, GraphicsUnit.Pixel);
        }
        catch { }
        return new Font(fallback, px, FontStyle.Regular, GraphicsUnit.Pixel);
    }
}

/// <summary>Uygulamanın kendi simgesi (exe içindeki), istenen boyutta.</summary>
public static class AppIcon
{
    static readonly Dictionary<int, Icon?> Cache = [];

    public static Icon? Get(int px)
    {
        if (Cache.TryGetValue(px, out var ic)) return ic;
        try { ic = Environment.ProcessPath is { } p ? Icon.ExtractIcon(p, 0, px) : null; }
        catch { ic = null; }
        return Cache[px] = ic;
    }

    public static void Paint(Graphics g, Rectangle r)
    {
        if (Get(r.Width) is { } ic) g.DrawIcon(ic, r);
    }
}
