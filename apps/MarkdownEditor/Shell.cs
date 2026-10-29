using System.Diagnostics;
using Microsoft.Win32;

namespace MarkdownEditor;

/// <summary>
/// Windows entegrasyonu (setup sürümü; Store sürümünde paket bildirimi yapar):
/// "Birlikte aç" listesine kayıt, varsayılan uygulama ayarları, Gezgin sağ tık menüsünde "PDF'e dönüştür".
/// Hepsi kullanıcı kayıt defterinde (HKCU), yönetici izni gerekmez.
/// </summary>
internal static class Shell
{
    public static readonly string[] Extensions = [".md", ".markdown", ".mdown", ".mkd", ".mkdn"];
    const string ProgId = "Grapekit.MarkdownEditor.md";
    static string Exe => Environment.ProcessPath ?? "";
    static string Classes => @"Software\Classes";

    /// <summary>Uygulamayı .md dosyaları için "Birlikte aç" ve varsayılan uygulamalar listesine ekler (her açılışta, ucuz ve zararsız).</summary>
    public static void RegisterFileTypes()
    {
        if (GrapekitApp.IsPackaged || string.IsNullOrEmpty(Exe)) return;
        try
        {
            using (var k = Registry.CurrentUser.CreateSubKey($@"{Classes}\{ProgId}"))
            {
                k.SetValue("", GrapekitApp.Lang == 0 ? "Markdown belgesi" : "Markdown document");
                k.SetValue("FriendlyTypeName", GrapekitApp.Lang == 0 ? "Markdown belgesi" : "Markdown document");
                using (var ic = k.CreateSubKey("DefaultIcon")) ic.SetValue("", $"\"{Exe}\",0");
                using var cmd = k.CreateSubKey(@"shell\open\command");
                cmd.SetValue("", $"\"{Exe}\" \"%1\"");
            }
            using (var app = Registry.CurrentUser.CreateSubKey($@"{Classes}\Applications\{Path.GetFileName(Exe)}"))
            {
                app.SetValue("FriendlyAppName", GrapekitApp.Name);
                using (var cmd = app.CreateSubKey(@"shell\open\command")) cmd.SetValue("", $"\"{Exe}\" \"%1\"");
                using var st = app.CreateSubKey("SupportedTypes");
                foreach (var e in Extensions) st.SetValue(e, "");
            }
            foreach (var e in Extensions)
            {
                using var owp = Registry.CurrentUser.CreateSubKey($@"{Classes}\{e}\OpenWithProgids");
                owp.SetValue(ProgId, Array.Empty<byte>(), RegistryValueKind.None);
            }
            // Varsayılan uygulamalar sayfasında görünmek için yetenekler
            using (var cap = Registry.CurrentUser.CreateSubKey(@"Software\Grapekit\MarkdownEditor\Capabilities"))
            {
                cap.SetValue("ApplicationName", GrapekitApp.Name);
                cap.SetValue("ApplicationDescription", GrapekitApp.Lang == 0 ? "Markdown düzenleyici ve görüntüleyici" : "Markdown editor and viewer");
                using var fa = cap.CreateSubKey("FileAssociations");
                foreach (var e in Extensions) fa.SetValue(e, ProgId);
            }
            using (var reg = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications"))
                reg.SetValue("Grapekit Markdown Editor", @"Software\Grapekit\MarkdownEditor\Capabilities");
            // Önceden açılmış Gezgin menüsü varsa yolunu tazele (uygulama taşınmış olabilir)
            if (ExplorerMenuEnabled()) SetExplorerMenu(true);
        }
        catch (Exception ex) { ErrorLog.Write(ex); }
    }

    /// <summary>Kurulum kaldırılırken çağrılır: kayıtları temizler.</summary>
    public static void Unregister()
    {
        try
        {
            SetExplorerMenu(false);
            Registry.CurrentUser.DeleteSubKeyTree($@"{Classes}\{ProgId}", false);
            Registry.CurrentUser.DeleteSubKeyTree($@"{Classes}\Applications\{Path.GetFileName(Exe)}", false);
            foreach (var e in Extensions) using (var k = Registry.CurrentUser.OpenSubKey($@"{Classes}\{e}\OpenWithProgids", true)) k?.DeleteValue(ProgId, false);
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Grapekit\MarkdownEditor", false);
            using (var reg = Registry.CurrentUser.OpenSubKey(@"Software\RegisteredApplications", true)) reg?.DeleteValue("Grapekit Markdown Editor", false);
            Notify();
        }
        catch { }
    }

    public static void OpenDefaultApps()
    {
        try { Process.Start(new ProcessStartInfo($"ms-settings:defaultapps?registeredAppUser={Uri.EscapeDataString("Grapekit Markdown Editor")}") { UseShellExecute = true }); }
        catch { Process.Start(new ProcessStartInfo("ms-settings:defaultapps") { UseShellExecute = true }); }
    }

    // --- Gezgin sağ tık: "PDF'e dönüştür" ---
    const string VerbKey = "Grapekit.MarkdownEditor.ToPdf";

    public static bool ExplorerMenuEnabled()
    {
        if (GrapekitApp.IsPackaged) return false;
        using var k = Registry.CurrentUser.OpenSubKey($@"{Classes}\SystemFileAssociations\.md\shell\{VerbKey}");
        return k != null;
    }

    public static void SetExplorerMenu(bool enabled)
    {
        if (GrapekitApp.IsPackaged) return;
        foreach (var e in Extensions)
        {
            var path = $@"{Classes}\SystemFileAssociations\{e}\shell\{VerbKey}";
            if (!enabled) { Registry.CurrentUser.DeleteSubKeyTree(path, false); continue; }
            using var k = Registry.CurrentUser.CreateSubKey(path);
            k.SetValue("MUIVerb", GrapekitApp.Lang == 0 ? "PDF'e dönüştür" : "Convert to PDF");
            k.SetValue("Icon", $"\"{Exe}\",0");
            k.SetValue("MultiSelectModel", "Player");
            using var c = k.CreateSubKey("command");
            c.SetValue("", $"\"{Exe}\" --to-pdf \"%1\"");
        }
        Notify();
    }

    static void Notify() => Native.SHChangeNotify(0x08000000 /* SHCNE_ASSOCCHANGED */, 0, IntPtr.Zero, IntPtr.Zero);
}
