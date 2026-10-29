using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace Grapekit;

/// <summary>
/// Uygulamanın Grapekit kimliği. Her uygulama Program.Main'in başında <see cref="Init"/> çağırır.
/// Hakkında penceresi, güncelleme denetimi ve standart menü buradan beslenir.
/// </summary>
public static class GrapekitApp
{
    /// <summary>Depodaki kısa ad (ör. "caps-lock"). Sürüm etiketleri ve güncelleme dosyası bu adı kullanır.</summary>
    public static string Id { get; private set; } = "app";
    /// <summary>Çalıştırılabilir dosyanın adı (uzantısız), ör. "CapsGosterge".</summary>
    public static string Exe { get; private set; } = "App";
    /// <summary>Uygulama adı; dil sırası: 0 = Türkçe, 1 = English, 2 = Deutsch.</summary>
    public static string[] Names { get; private set; } = ["Uygulama", "App", "App"];
    /// <summary>grapekit.com'daki sayfa adresleri (Türkçe, İngilizce).</summary>
    public static string SlugTr { get; private set; } = "";
    public static string SlugEn { get; private set; } = "";
    /// <summary>Etkin arayüz dili: 0 = Türkçe, 1 = English, 2 = Deutsch.</summary>
    public static int Lang { get; set; }

    public static readonly bool IsPackaged = DetectPackaged();

    public static Version Version => Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(1, 0, 0);
    public static string VersionText => $"{Version.Major}.{Version.Minor}.{Version.Build}";
    public static string Name => Names[Math.Clamp(Lang, 0, Names.Length - 1)];

    /// <summary>Güncelleme bilgisi: GitHub Actions her sürümde depodaki bu dosyayı günceller.</summary>
    public static string UpdateManifestUrl => $"{Brand.RawBase}/updates/{Id}.json";

    public static string PageUrl => Lang == 0 ? $"{Brand.Website}/uygulama/{SlugTr}" : $"{Brand.Website}/en/app/{SlugEn}";
    public static string SourceUrl => $"{Brand.GitHub}/tree/main/apps/{Exe}";
    public static string IssuesUrl => $"{Brand.GitHub}/issues/new?labels={Uri.EscapeDataString(Id)}&title={Uri.EscapeDataString($"[{Names[1]}] ")}";

    public static void Init(string id, string exe, string[] names, string slugTr, string slugEn)
    {
        Id = id; Exe = exe; Names = names; SlugTr = slugTr; SlugEn = slugEn;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern int GetCurrentPackageFullName(ref int len, StringBuilder? name);

    static bool DetectPackaged()
    {
        try
        {
            int len = 0;
            return GetCurrentPackageFullName(ref len, null) != 15700; // APPMODEL_ERROR_NO_PACKAGE
        }
        catch { return false; }
    }
}
