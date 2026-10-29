namespace Grapekit;

/// <summary>Tüm Grapekit uygulamalarında ortak metinler (0 = Türkçe, 1 = English, 2 = Deutsch).</summary>
public static class CoreText
{
    static readonly Dictionary<string, string[]> M = new()
    {
        ["support"] = ["☕ Bir kahve ısmarla", "☕ Buy us a coffee", "☕ Spendier uns einen Kaffee"],
        ["support_desc"] = ["Tüm Grapekit uygulamaları ücretsiz kalacak. Bir kahve ısmarlayarak yenilerinin yapılmasına destek olabilirsiniz.",
                            "Every Grapekit app will stay free. Buying us a coffee helps us build the next one.",
                            "Alle Grapekit-Apps bleiben kostenlos. Mit einem Kaffee unterstützt du neue Tools."],
        ["more_apps"] = ["Grapekit'teki diğer araçlar", "More tools from Grapekit", "Weitere Tools von Grapekit"],
        ["about"] = ["Hakkında", "About", "Über"],
        ["website"] = ["Uygulama sayfası", "App page", "App-Seite"],
        ["source"] = ["Kaynak kodu (GitHub)", "Source code (GitHub)", "Quellcode (GitHub)"],
        ["report"] = ["Hata bildir / öneri", "Report a bug / idea", "Fehler melden / Idee"],
        ["privacy"] = ["Gizlilik", "Privacy", "Datenschutz"],
        ["tagline"] = ["Windows için küçük, kullanışlı araçlar", "Small, handy tools for Windows", "Kleine, praktische Tools für Windows"],
        ["by"] = ["Grapekit tarafından geliştirildi", "Made by Grapekit", "Entwickelt von Grapekit"],
        ["free_note"] = ["Ücretsiz · Reklamsız · Açık kaynak (MIT)", "Free · No ads · Open source (MIT)", "Kostenlos · Werbefrei · Open Source (MIT)"],
        ["privacy_note"] = ["Hiçbir kişisel veri toplanmaz. Güncelleme denetimi yalnızca sürüm numarasını okur.",
                            "No personal data is collected. The update check only reads the latest version number.",
                            "Es werden keine persönlichen Daten erhoben. Die Update-Prüfung liest nur die Versionsnummer."],
    };

    public static string T(string key) => M.TryGetValue(key, out var v) ? v[Math.Clamp(GrapekitApp.Lang, 0, 2)] : key;
}
