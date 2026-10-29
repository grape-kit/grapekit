using System.Text.Json;
using System.Text.Json.Nodes;

namespace MarkdownEditor;

/// <summary>
/// Kalıcı veriler (%APPDATA%\MarkdownEditor): ayarlar ve oturum JSON olarak, kurtarma kopyaları ayrı klasörde, son dosyalar listesi.
/// Yazmalar atomiktir (geçici dosya + taşıma): yarıda kalan yazma eski dosyayı bozmaz.
/// </summary>
internal static class Store
{
    static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };
    static string Dir => SettingsFile.Dir;
    static string P(string name) => Path.Combine(Dir, name);
    static string RecoveryDir => Path.Combine(Dir, "kurtarma");

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

    /// <summary>Kabuğun da okuduğu birkaç ayar.</summary>
    public static bool Bool(string key, bool fallback)
    {
        try { return LoadSettings()?[key]?.GetValue<bool>() ?? fallback; } catch { return fallback; }
    }
    public static string Str(string key, string fallback)
    {
        try { return LoadSettings()?[key]?.GetValue<string>() is { Length: > 0 } v ? v : fallback; } catch { return fallback; }
    }

    // --- Oturum ---
    public static JsonNode? LoadSession() => ReadJson("session.json");
    public static void SaveSession(JsonNode? s) { if (s != null) WriteAtomic(P("session.json"), s.ToJsonString(Indented)); }

    // --- Pencere konumu ---
    public sealed record WindowPlacement(int X, int Y, int W, int H, bool Max);
    public static WindowPlacement? LoadWindow()
    {
        try { var f = P("window.json"); return File.Exists(f) ? JsonSerializer.Deserialize<WindowPlacement>(File.ReadAllText(f)) : null; }
        catch { return null; }
    }
    public static void SaveWindow(WindowPlacement w) { try { WriteAtomic(P("window.json"), JsonSerializer.Serialize(w)); } catch { } }

    // --- Kurtarma ---
    public static void SaveRecovery(JsonArray items)
    {
        Directory.CreateDirectory(RecoveryDir);
        var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var it in items)
        {
            if (it is null) continue;
            var id = it["id"]?.GetValue<string>() ?? Guid.NewGuid().ToString("N");
            var name = string.Concat(id.Where(char.IsLetterOrDigit)) + ".json";
            keep.Add(name);
            WriteAtomic(Path.Combine(RecoveryDir, name), it.ToJsonString());
        }
        foreach (var f in Directory.GetFiles(RecoveryDir, "*.json"))
            if (!keep.Contains(Path.GetFileName(f))) TryDelete(f);
    }

    public static JsonArray ListRecovery()
    {
        var arr = new JsonArray();
        if (!Directory.Exists(RecoveryDir)) return arr;
        foreach (var f in Directory.GetFiles(RecoveryDir, "*.json").OrderBy(File.GetLastWriteTimeUtc))
        {
            try { if (JsonNode.Parse(File.ReadAllText(f)) is { } n) arr.Add(n); } catch { }
        }
        return arr;
    }

    public static void ClearRecovery()
    {
        if (!Directory.Exists(RecoveryDir)) return;
        foreach (var f in Directory.GetFiles(RecoveryDir)) TryDelete(f);
    }

    static void TryDelete(string f) { try { File.Delete(f); } catch { } }

    // --- Son dosyalar ---
    sealed record Recent(string Path, bool Pinned);
    static List<Recent> LoadRecent()
    {
        try { var f = P("recent.json"); return File.Exists(f) ? JsonSerializer.Deserialize<List<Recent>>(File.ReadAllText(f)) ?? [] : []; }
        catch { return []; }
    }
    static void SaveRecent(List<Recent> list) => WriteAtomic(P("recent.json"), JsonSerializer.Serialize(list));

    public static JsonArray RecentList()
    {
        var list = LoadRecent().Where(r => File.Exists(r.Path)).OrderByDescending(r => r.Pinned).ToList();
        var arr = new JsonArray();
        foreach (var r in list) arr.Add(new JsonObject { ["path"] = r.Path, ["pinned"] = r.Pinned });
        return arr;
    }

    public static void AddRecent(string path)
    {
        var list = LoadRecent();
        var old = list.FirstOrDefault(r => r.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
        list.RemoveAll(r => r.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, new Recent(path, old?.Pinned ?? false));
        var pinned = list.Where(r => r.Pinned);
        var rest = list.Where(r => !r.Pinned).Take(30);
        SaveRecent([.. pinned, .. rest]);
        Native.AddToRecentDocs(path); // Başlat menüsü / görev çubuğu atlama listesi
    }

    public static void RemoveRecent(string path)
    {
        var list = LoadRecent();
        list.RemoveAll(r => r.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
        SaveRecent(list);
    }

    public static void PinRecent(string path, bool pinned)
    {
        var list = LoadRecent().Select(r => r.Path.Equals(path, StringComparison.OrdinalIgnoreCase) ? r with { Pinned = pinned } : r).ToList();
        SaveRecent(list);
    }
}
