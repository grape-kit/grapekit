using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace Grapekit;

/// <summary>
/// setup.exe ile kurulan sürüm için otomatik güncelleme.
/// Microsoft Store sürümünde devre dışıdır (güncellemeleri Store yapar).
///
/// version.json biçimi:
/// { "version": "1.1.0",
///   "notes": { "tr": "...", "en": "...", "de": "..." },
///   "files": { "x64":   { "url": "https://.../CapsGosterge-Setup-1.1.0-x64.exe",   "sha256": "..." },
///              "x86":   { ... }, "arm64": { ... } } }
/// </summary>
public static class Updater
{
    public sealed record Info(Version Version, string Url, string Sha256, string Notes);

    public static bool Supported => !GrapekitApp.IsPackaged;

    static string Arch => RuntimeInformation.ProcessArchitecture switch
    {
        Architecture.Arm64 => "arm64",
        Architecture.X86 => "x86",
        _ => "x64",
    };

    static HttpClient Client()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd($"{GrapekitApp.Exe}/{GrapekitApp.VersionText}");
        return c;
    }

    /// <returns>Yeni sürüm varsa bilgisi, yoksa null. Ağ hatasında istisna fırlatır.</returns>
    public static async Task<Info?> CheckAsync()
    {
        if (!Supported) return null;
        using var c = Client();
        using var resp = await c.GetAsync(GrapekitApp.UpdateManifestUrl);
        // Henüz hiç yayımlanmamış uygulama: bilgi dosyası yok, yani güncelleme de yok (bağlantı hatası değil)
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var ver = Version.Parse(root.GetProperty("version").GetString() ?? "0.0.0");
        if (ver <= GrapekitApp.Version) return null;

        var file = root.GetProperty("files").GetProperty(Arch);
        string notes = "";
        if (root.TryGetProperty("notes", out var n))
        {
            string lang = GrapekitApp.Lang switch { 0 => "tr", 2 => "de", _ => "en" };
            if (n.TryGetProperty(lang, out var t) || n.TryGetProperty("en", out t)) notes = t.GetString() ?? "";
        }
        return new Info(ver, file.GetProperty("url").GetString()!, file.GetProperty("sha256").GetString()!, notes);
    }

    public static async Task<string> DownloadAsync(Info info, IProgress<int>? progress)
    {
        if (!info.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Güvenli olmayan adres.");

        var path = Path.Combine(Path.GetTempPath(), $"{GrapekitApp.Exe}-Setup-{info.Version}.exe");
        using var c = Client();
        c.Timeout = TimeSpan.FromMinutes(10);
        using var resp = await c.GetAsync(info.Url, HttpCompletionOption.ResponseHeadersRead);
        resp.EnsureSuccessStatusCode();
        long total = resp.Content.Headers.ContentLength ?? -1;

        await using (var src = await resp.Content.ReadAsStreamAsync())
        await using (var dst = File.Create(path))
        {
            var buf = new byte[81920];
            long done = 0;
            int read, last = -1;
            while ((read = await src.ReadAsync(buf)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, read));
                done += read;
                // Yalnızca yüzde değişince bildir: arayüz her 80 KB'ta yeniden çizilmesin
                int pct = total > 0 ? (int)(done * 100 / total) : -1;
                if (pct != last && pct >= 0) { last = pct; progress?.Report(pct); }
            }
        }

        await using (var fs = File.OpenRead(path))
        {
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(fs));
            if (!hash.Equals(info.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(path);
                throw new InvalidOperationException("İndirilen dosya doğrulanamadı (SHA-256 uyuşmuyor).");
            }
        }
        return path;
    }

    /// <summary>Kurulumu sessizce başlatır; çağıran program hemen kapanmalıdır. Kurulum bitince program yeniden açılır.</summary>
    public static void RunInstaller(string path)
    {
        Process.Start(new ProcessStartInfo(path, "/SILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS")
        {
            UseShellExecute = true,
        });
    }
}
