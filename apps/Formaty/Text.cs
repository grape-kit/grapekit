namespace Formaty;

/// <summary>Kabuk tarafında kullanıcıya görünen kısa metinler (hata mesajları). Arayüz metinleri web/src/i18n.js'de.</summary>
internal static class Text
{
    static readonly Dictionary<string, (string Tr, string En)> S = new()
    {
        ["err.unreadable"] = ("Dosya okunamadı ya da bu biçim desteklenmiyor.", "The file can't be read or the format isn't supported."),
        ["err.convert"] = ("Dönüştürme başarısız oldu.", "Conversion failed."),
        ["err.format"] = ("Bu hedef biçim bu dosya için kullanılamaz.", "This target format can't be used for this file."),
        ["err.noAudio"] = ("Bu dosyada ses yok.", "This file has no audio."),
        ["err.noVideo"] = ("Bu dosyada görüntü yok.", "This file has no video."),
        ["err.pdfPassword"] = ("PDF şifreli: doğru şifreyi girin.", "The PDF is password-protected: enter the correct password."),
        ["err.pdfOpen"] = ("PDF açılamadı (bozuk ya da desteklenmeyen).", "The PDF can't be opened (damaged or unsupported)."),
        ["err.pdfRange"] = ("Sayfa aralığı geçersiz. Örnek: 1-3, 5, 8-", "Invalid page range. Example: 1-3, 5, 8-"),
        ["err.engine"] = ("Dönüştürme motoru bulunamadı. Formaty'yi yeniden kurun.", "Conversion engine not found. Please reinstall Formaty."),
        ["err.same"] = ("Kaynak ve hedef aynı dosya olamaz.", "Source and target can't be the same file."),
        ["err.disk"] = ("Diskte yer kalmadı.", "The disk is full."),
        ["err.access"] = ("Dosyaya erişilemedi (başka bir programda açık ya da izin yok).", "The file can't be accessed (open in another program or no permission)."),
        ["skip.exists"] = ("Hedef dosya zaten var, atlandı.", "The target already exists, skipped."),
        ["sub.default"] = ("Formaty", "Formaty"),
    };

    public static string T(string key) => S.TryGetValue(key, out var v) ? (GrapekitApp.Lang == 0 ? v.Tr : v.En) : key;
}
