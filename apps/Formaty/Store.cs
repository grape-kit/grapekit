using System.Text.Json;
using System.Text.Json.Nodes;

namespace Formaty;

/// <summary>
/// Kalıcı veriler (%APPDATA%\Formaty): ayarlar ve kullanıcı hazır ayarları JSON olarak, pencere konumu.
/// Yazmalar atomiktir (geçici dosya + taşıma): yarıda kalan yazma eski dosyayı bozmaz.
/// </summary>
internal static class Store
{
    static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };
    static string P(string name) => Path.Combine(SettingsFile.Dir, name);

    public static void WriteAtomic(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, text);
        File.Move(tmp, path, overwrite: true);
    }

    static JsonNode? ReadJson(string name)
    {
        try { var f = P(name); return File.Exists(f) ? JsonNode.Parse(File.ReadAllText(f)) : null; }
        catch { return null; }
    }

    // --- Ayarlar (arayüzün nesnesi olduğu gibi saklanır) ---
    public static JsonNode? LoadSettings() => ReadJson("settings.json");
    public static void SaveSettings(JsonNode? s) { if (s != null) WriteAtomic(P("settings.json"), s.ToJsonString(Indented)); }

    public static bool Bool(string key, bool fallback)
    {
        try { return LoadSettings()?[key]?.GetValue<bool>() ?? fallback; } catch { return fallback; }
    }
    public static string Str(string key, string fallback)
    {
        try { return LoadSettings()?[key]?.GetValue<string>() ?? fallback; } catch { return fallback; }
    }

    // --- Pencere ---
    public sealed record WindowPlacement(int X, int Y, int W, int H, bool Max);
    public static WindowPlacement? LoadWindow()
    {
        try { var f = P("window.json"); return File.Exists(f) ? JsonSerializer.Deserialize<WindowPlacement>(File.ReadAllText(f)) : null; }
        catch { return null; }
    }
    public static void SaveWindow(WindowPlacement w) { try { WriteAtomic(P("window.json"), JsonSerializer.Serialize(w)); } catch { } }
}
