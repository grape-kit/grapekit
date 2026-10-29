using Microsoft.Win32;

namespace Grapekit.Ui;

public sealed record Palette(
    bool Dark,
    Color Bg,
    Color Card,
    Color Border,
    Color Text,
    Color SubText,
    Color Accent,
    Color OnAccent,
    Color AccentText,
    Color Neutral,
    Color Hover,
    Color Warning,
    bool HighContrast = false);

public static class ThemeInfo
{
    const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>Hazır renk seçenekleri (Görünüm sekmesi).</summary>
    public static readonly int[] Swatches =
    [
        unchecked((int)0xFF0067C0), unchecked((int)0xFF0099BC), unchecked((int)0xFF00A86B), unchecked((int)0xFF6B9E1F),
        unchecked((int)0xFFE3A400), unchecked((int)0xFFF7630C), unchecked((int)0xFFE81123), unchecked((int)0xFFC30052),
        unchecked((int)0xFF8E44D8), unchecked((int)0xFF5D5A58),
    ];

    static int ReadDword(string key, string name, int fallback)
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(key);
            return k?.GetValue(name) is int v ? v : fallback;
        }
        catch { return fallback; }
    }

    public static bool AppsUseLight() => ReadDword(PersonalizeKey, "AppsUseLightTheme", 1) != 0;
    public static bool TaskbarUsesLight() => ReadDword(PersonalizeKey, "SystemUsesLightTheme", 0) != 0;
    public static bool TaskbarUsesAccent() => ReadDword(PersonalizeKey, "ColorPrevalence", 0) != 0;

    public static Color SystemAccent()
    {
        int v = ReadDword(@"Software\Microsoft\Windows\DWM", "AccentColor", unchecked((int)0xFFC06700));
        return Color.FromArgb(255, v & 0xFF, (v >> 8) & 0xFF, (v >> 16) & 0xFF);
    }

    public static Palette Get(int themeSetting, int accentArgb)
    {
        if (SystemInformation.HighContrast)
        {
            return new Palette(Luminance(SystemColors.Window) < 0.5,
                Bg: SystemColors.Window, Card: SystemColors.Window, Border: SystemColors.WindowText,
                Text: SystemColors.WindowText, SubText: SystemColors.WindowText,
                Accent: SystemColors.Highlight, OnAccent: SystemColors.HighlightText, AccentText: SystemColors.HotTrack,
                Neutral: SystemColors.Window, Hover: SystemColors.Highlight, Warning: SystemColors.Highlight,
                HighContrast: true);
        }

        bool dark = themeSetting switch { 1 => false, 2 => true, _ => !AppsUseLight() };
        var accent = accentArgb == 0 ? SystemAccent() : Color.FromArgb(255, Color.FromArgb(accentArgb));
        var onAccent = Luminance(accent) > 0.55 ? Color.FromArgb(20, 20, 20) : Color.White;

        return dark
            ? new Palette(true,
                Bg: C(0x202020), Card: C(0x2B2B2B), Border: C(0x3A3A3A),
                Text: C(0xFFFFFF), SubText: C(0xABABAB),
                Accent: accent, OnAccent: onAccent, AccentText: Mix(accent, Color.White, 0.40),
                Neutral: C(0x3C3C3C), Hover: C(0x353535), Warning: C(0xFCB93F))
            : new Palette(false,
                Bg: C(0xF3F3F3), Card: C(0xFFFFFF), Border: C(0xE3E3E3),
                Text: C(0x1B1B1B), SubText: C(0x616161),
                Accent: accent, OnAccent: onAccent, AccentText: Luminance(accent) > 0.5 ? Mix(accent, Color.Black, 0.45) : accent,
                Neutral: C(0xEAEAEA), Hover: C(0xF5F5F5), Warning: C(0x9D5D00));
    }

    public static Color C(int rgb) => Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);

    public static Color Mix(Color a, Color b, double t) => Color.FromArgb(
        (int)Math.Round(a.A + (b.A - a.A) * t),
        (int)Math.Round(a.R + (b.R - a.R) * t),
        (int)Math.Round(a.G + (b.G - a.G) * t),
        (int)Math.Round(a.B + (b.B - a.B) * t));

    public static double Luminance(Color c) => (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;
}
