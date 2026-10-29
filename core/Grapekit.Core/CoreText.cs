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
        ["settings"] = ["Ayarlar", "Settings", "Einstellungen"],
        ["quit"] = ["Çıkış", "Quit", "Beenden"],
        ["minimize"] = ["Simge durumuna küçült", "Minimize", "Minimieren"],
        ["close"] = ["Kapat", "Close", "Schließen"],
        ["version"] = ["Sürüm {0}", "Version {0}", "Version {0}"],
        ["upd_check"] = ["Güncellemeleri denetle", "Check for updates", "Nach Updates suchen"],
        ["upd_checking"] = ["Denetleniyor…", "Checking…", "Wird geprüft…"],
        ["upd_latest"] = ["En yeni sürümü kullanıyorsun.", "You're on the latest version.", "Du verwendest die neueste Version."],
        ["upd_found"] = ["Yeni sürüm hazır: {0}", "New version available: {0}", "Neue Version verfügbar: {0}"],
        ["upd_install"] = ["{0} sürümüne güncelle", "Update to {0}", "Auf {0} aktualisieren"],
        ["upd_downloading"] = ["İndiriliyor… %{0}", "Downloading… {0}%", "Wird geladen… {0} %"],
        ["upd_error"] = ["Denetlenemedi. İnternet bağlantını kontrol et.", "Couldn't check. Please check your internet connection.", "Prüfung fehlgeschlagen. Bitte Internetverbindung prüfen."],
        ["upd_failed"] = ["Güncelleme indirilemedi: {0}", "The update couldn't be downloaded: {0}", "Update konnte nicht geladen werden: {0}"],
        ["upd_last"] = ["Son denetim: {0}", "Last checked: {0}", "Zuletzt geprüft: {0}"],
        ["upd_never"] = ["Henüz denetlenmedi.", "Not checked yet.", "Noch nicht geprüft."],
        ["upd_store"] = ["Güncellemeler Microsoft Store üzerinden gelir.", "Updates come through the Microsoft Store.", "Updates kommen über den Microsoft Store."],
        ["upd_auto"] = ["Güncellemeleri kendiliğinden denetle", "Check for updates automatically", "Automatisch nach Updates suchen"],
        ["upd_auto_desc"] = ["Günde bir kez yalnızca sürüm numarasına bakar", "Once a day, only reads the version number", "Einmal täglich, liest nur die Versionsnummer"],
        ["balloon_update_title"] = ["Yeni sürüm hazır", "Update available", "Update verfügbar"],
        ["balloon_update_text"] = ["{0} {1} indirilebilir. Ayrıntılar için tıkla.", "{0} {1} is available. Click for details.", "{0} {1} ist verfügbar. Für Details klicken."],
    };

    static readonly System.Globalization.CultureInfo[] Cultures = [new("tr-TR"), new("en-US"), new("de-DE")];
    public static System.Globalization.CultureInfo Culture => Cultures[Math.Clamp(GrapekitApp.Lang, 0, 2)];

    /// <summary>Sistem dilinden uygulama dili: Türkçe değilse İngilizce.</summary>
    public static int SystemLang() => System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tr" ? 0 : 1;

    public static string T(string key) => M.TryGetValue(key, out var v) ? v[Math.Clamp(GrapekitApp.Lang, 0, 2)] : key;
}
