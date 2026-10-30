using Microsoft.Win32;

namespace Formaty;

/// <summary>
/// Gezgin entegrasyonu (kurulum sürümü; Store sürümünde paket bildirimi yapar): fotoğraf, video, ses ve PDF dosyalarında
/// sağ tık › "Formaty ile dönüştür" alt menüsü. Hepsi kullanıcı kayıt defterinde (HKCU), yönetici izni gerekmez.
/// Windows 11'de "Daha fazla seçenek göster" altında görünür.
/// </summary>
internal static class Shell
{
    const string Key = "Formaty";
    static string Exe => Environment.ProcessPath ?? "";
    const string Classes = @"Software\Classes";

    // Tür → (eylem kodu, TR, EN). "open" uygulamayı dosyalarla açar.
    static readonly (string Assoc, (string Act, string Tr, string En)[] Items)[] Menus =
    [
        ("image", [("img:jpg", "JPG'ye çevir", "Convert to JPG"), ("img:png", "PNG'ye çevir", "Convert to PNG"), ("img:webp", "WebP'ye çevir", "Convert to WebP"),
                   ("img:avif", "AVIF'e çevir", "Convert to AVIF"), ("img:pdf", "PDF yap", "Make PDF"), ("img:small", "Boyutunu küçült", "Make smaller")]),
        ("video", [("vid:mp4", "MP4'e çevir", "Convert to MP4"), ("vid:mp3", "Sesini çıkar (MP3)", "Extract audio (MP3)"),
                   ("vid:compress", "Sıkıştır", "Compress"), ("vid:gif", "GIF yap", "Make GIF")]),
        ("audio", [("aud:mp3", "MP3'e çevir", "Convert to MP3"), ("aud:wav", "WAV'a çevir", "Convert to WAV"), ("aud:flac", "FLAC'a çevir", "Convert to FLAC"), ("aud:aac", "M4A'ya çevir", "Convert to M4A")]),
        (".pdf", [("pdf:jpg", "Görsellere çevir (JPG)", "Convert to images (JPG)"), ("pdf:compress", "Küçült", "Compress"), ("pdf:merge", "Birleştir", "Merge")]),
    ];

    public static bool MenuEnabled()
    {
        if (GrapekitApp.IsPackaged) return false;
        using var k = Registry.CurrentUser.OpenSubKey($@"{Classes}\SystemFileAssociations\image\shell\{Key}");
        return k != null;
    }

    /// <summary>Her açılışta: kullanıcı kapatmadıysa menüyü (güncel yol ve dille) yazar.</summary>
    public static void Register()
    {
        if (GrapekitApp.IsPackaged || string.IsNullOrEmpty(Exe)) return;
        try
        {
            var choice = Registry.CurrentUser.OpenSubKey(@"Software\Grapekit\Formaty")?.GetValue("ExplorerMenu") as int?;
            if (choice != 0) SetMenu(true, remember: false);
        }
        catch (Exception ex) { ErrorLog.Write(ex); }
    }

    public static void SetMenu(bool enabled, bool remember = true)
    {
        if (GrapekitApp.IsPackaged || string.IsNullOrEmpty(Exe)) return;
        bool tr = GrapekitApp.Lang == 0;
        foreach (var (assoc, items) in Menus)
        {
            var root = $@"{Classes}\SystemFileAssociations\{assoc}\shell\{Key}";
            Registry.CurrentUser.DeleteSubKeyTree(root, false);
            if (!enabled) continue;
            using var k = Registry.CurrentUser.CreateSubKey(root);
            k.SetValue("MUIVerb", tr ? "Formaty ile dönüştür" : "Convert with Formaty");
            k.SetValue("Icon", $"\"{Exe}\",0");
            k.SetValue("SubCommands", "");
            k.SetValue("MultiSelectModel", "Player");
            int n = 1;
            foreach (var (act, t, e) in items.Append(("open", "Diğer seçenekler…", "More options…")))
            {
                using var c = k.CreateSubKey($@"shell\{n++:00}{act.Replace(':', '_')}");
                c.SetValue("MUIVerb", tr ? t : e);
                c.SetValue("MultiSelectModel", "Player");
                if (act == "open") c.SetValue("CommandFlags", 0x20, RegistryValueKind.DWord); // üstüne ayraç
                using var cmd = c.CreateSubKey("command");
                cmd.SetValue("", act == "open" ? $"\"{Exe}\" \"%1\"" : $"\"{Exe}\" --action {act} \"%1\"");
            }
        }
        if (remember) using (var s = Registry.CurrentUser.CreateSubKey(@"Software\Grapekit\Formaty")) s.SetValue("ExplorerMenu", enabled ? 1 : 0, RegistryValueKind.DWord);
        Native.SHChangeNotify(0x08000000 /* SHCNE_ASSOCCHANGED */, 0, IntPtr.Zero, IntPtr.Zero);
    }

    /// <summary>Kurulum kaldırılırken çağrılır.</summary>
    public static void Unregister()
    {
        try
        {
            SetMenu(false, remember: false);
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Grapekit\Formaty", false);
        }
        catch { }
    }
}
