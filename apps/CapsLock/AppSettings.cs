using System.Text.Json;

namespace CapsGosterge;

internal enum LockKey { Caps = 0, Num = 1, Scroll = 2 }

internal sealed class AppSettings
{
    // ── Genel
    /// <summary>0 = Türkçe, 1 = English, 2 = Deutsch, -1 = sistem dilinden alınır</summary>
    public int Language { get; set; } = -1;
    public bool AutoStart { get; set; } = true;
    public bool WatchCaps { get; set; } = true;
    public bool WatchNum { get; set; } = true;
    public bool WatchScroll { get; set; }
    public bool EfficiencyMode { get; set; } = true;

    // ── Görünüm
    /// <summary>0 = sistem, 1 = açık, 2 = koyu</summary>
    public int Theme { get; set; }
    /// <summary>0 = Windows vurgu rengi, aksi halde ARGB</summary>
    public int AccentArgb { get; set; }
    /// <summary>0 küçük, 1 orta, 2 büyük, 3 çok büyük</summary>
    public int CardSize { get; set; } = 1;
    public int Opacity { get; set; } = 100;

    // ── Bildirim
    public bool ShowOsd { get; set; } = true;
    /// <summary>3x3 ızgara, satır satır: 0 sol üst … 4 tam orta … 8 sağ alt</summary>
    public int Position { get; set; } = 7;
    public int DurationMs { get; set; } = 1500;
    public bool ShowBadge { get; set; }

    // ── Ses
    public bool Sound { get; set; }
    /// <summary>0 yumuşak, 1 zil, 2 tık, 3 piyano, 4 mekanik, 5 damla, 6 özel</summary>
    public int SoundPack { get; set; }
    public int VolumePercent { get; set; } = 50;
    public string CustomOnWav { get; set; } = "";
    public string CustomOffWav { get; set; } = "";

    // ── Sesli okuma
    public bool Speak { get; set; }
    public int SpeakRate { get; set; } = 1; // 0 yavaş, 1 normal, 2 hızlı

    // ── Yanlışlıkla basma koruması
    /// <summary>0 kapalı, 1 basılı tut, 2 çift bas</summary>
    public int Protection { get; set; }
    public int HoldMs { get; set; } = 500;
    public int DoubleMs { get; set; } = 400;
    public bool SinglePressTurnsOff { get; set; } = true;
    public bool ShowProtectionHint { get; set; } = true;

    // ── Şifre uyarısı
    public bool PasswordWarning { get; set; } = true;
    public bool PasswordWarningSound { get; set; } = true;

    // ── Kısayol
    public bool HotkeyEnabled { get; set; } = true;
    /// <summary>Alt 16 bit: sanal tuş, üst 16 bit: MOD_* bayrakları. Varsayılan Ctrl+Alt+C.</summary>
    public int Hotkey { get; set; } = (int)((Native.MOD_CONTROL | Native.MOD_ALT) << 16 | 'C');

    // ── Sessiz saatler
    public bool QuietHours { get; set; }
    public int QuietStartMin { get; set; } = 22 * 60;
    public int QuietEndMin { get; set; } = 8 * 60;
    /// <summary>0 = sadece ses ve sesli okuma, 1 = tüm bildirimler</summary>
    public int QuietMode { get; set; }
    public bool QuietWhenFullscreen { get; set; } = true;

    // ── İstatistik
    public bool Stats { get; set; } = true;

    // ── Güncelleme (yalnızca setup.exe sürümü)
    public bool AutoUpdateCheck { get; set; } = true;
    public string SkippedVersion { get; set; } = "";
    public DateTime LastUpdateCheck { get; set; }

    public const int MinDuration = 500, MaxDuration = 10000, DurationStep = 500;

    public static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CapsGosterge");
    static string FilePath => Path.Combine(Dir, "ayarlar.json");

    public static bool Exists => File.Exists(FilePath);

    public static AppSettings Load()
    {
        AppSettings s;
        try
        {
            s = File.Exists(FilePath)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings()
                : new AppSettings();
        }
        catch { s = new AppSettings(); }
        s.Normalize();
        return s;
    }

    public void Normalize()
    {
        if (Language is < 0 or > 2) Language = L.FromSystem();
        Theme = Math.Clamp(Theme, 0, 2);
        CardSize = Math.Clamp(CardSize, 0, 3);
        Opacity = Math.Clamp(Opacity, 30, 100);
        Position = Math.Clamp(Position, 0, 8);
        DurationMs = Math.Clamp(DurationMs, MinDuration, MaxDuration);
        SoundPack = Math.Clamp(SoundPack, 0, 6);
        VolumePercent = Math.Clamp(VolumePercent, 0, 100);
        SpeakRate = Math.Clamp(SpeakRate, 0, 2);
        Protection = Math.Clamp(Protection, 0, 2);
        HoldMs = Math.Clamp(HoldMs, 200, 2000);
        DoubleMs = Math.Clamp(DoubleMs, 200, 1000);
        QuietStartMin = Math.Clamp(QuietStartMin, 0, 1439);
        QuietEndMin = Math.Clamp(QuietEndMin, 0, 1439);
        QuietMode = Math.Clamp(QuietMode, 0, 1);
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tmp, FilePath, overwrite: true);
        }
        catch { }
    }

    public bool Watches(LockKey k) => k switch
    {
        LockKey.Caps => WatchCaps,
        LockKey.Num => WatchNum,
        _ => WatchScroll,
    };

    /// <summary>Şu an sessiz saat aralığında mıyız? (Gece yarısını aşan aralıklar desteklenir.)</summary>
    public bool InQuietHours(DateTime now)
    {
        if (!QuietHours || QuietStartMin == QuietEndMin) return false;
        int m = now.Hour * 60 + now.Minute;
        return QuietStartMin < QuietEndMin
            ? m >= QuietStartMin && m < QuietEndMin
            : m >= QuietStartMin || m < QuietEndMin;
    }
}
