namespace KeepAwake;

internal sealed class Settings
{
    /// <summary>0 = Türkçe, 1 = English, -1 = sistem dilinden alınır</summary>
    public int Language { get; set; } = -1;
    /// <summary>0 = sistem, 1 = açık, 2 = koyu</summary>
    public int Theme { get; set; }
    /// <summary>0 = yalnızca sistem uyanık, 1 = sistem ve ekran</summary>
    public int Mode { get; set; } = 1;
    /// <summary>Başlat'ın süresi (dakika, hazır seçeneklerden); 0 = süresiz</summary>
    public int Minutes { get; set; } = 60;
    /// <summary>Özel süre seçili mi (CustomMinutes kullanılır)</summary>
    public bool Custom { get; set; }
    /// <summary>Özel süre (saniye), kullanıcı serbestçe yazar</summary>
    public int CustomSeconds { get; set; } = 90 * 60;
    /// <summary>Süreli oturum bitince: 0 hiçbir şey, 1 ekranı kapat, 2 kilitle, 3 uyku, 4 bilgisayarı kapat</summary>
    public int EndAction { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public int EffectiveSeconds => Custom ? CustomSeconds : Minutes * 60;

    public bool AutoStart { get; set; } = true;
    public bool StartActive { get; set; }
    public bool NotifyOnEnd { get; set; } = true;

    // ── Kurallar
    public List<string> Processes { get; set; } = [];
    public bool OnlyOnAC { get; set; }
    public bool PauseOnLowBattery { get; set; } = true;
    public int LowBatteryPercent { get; set; } = 20;
    public bool Fullscreen { get; set; }

    // ── Kısayol: alt 16 bit sanal tuş, üst 16 bit MOD_* bayrakları. Varsayılan Ctrl+Alt+U.
    public bool HotkeyEnabled { get; set; } = true;
    public int Hotkey { get; set; } = (int)((Win32.MOD_CONTROL | Win32.MOD_ALT) << 16 | 'U');

    // ── Güncelleme (yalnızca setup.exe sürümü)
    public bool AutoUpdateCheck { get; set; } = true;
    public DateTime LastUpdateCheck { get; set; }

    public static Settings Load()
    {
        var s = SettingsFile.Load<Settings>();
        if (s.Language is < 0 or > 1) s.Language = CoreText.SystemLang();
        s.Theme = Math.Clamp(s.Theme, 0, 2);
        s.Mode = Math.Clamp(s.Mode, 0, 1);
        if (!L.Durations.Contains(s.Minutes)) s.Minutes = 60;
        if (s.CustomSeconds < 1) s.CustomSeconds = 90 * 60;
        s.EndAction = Math.Clamp(s.EndAction, 0, 4);
        s.LowBatteryPercent = Math.Clamp(s.LowBatteryPercent, 5, 50);
        s.Processes = s.Processes.Select(Normalize).Where(p => p.Length > 0).Distinct().ToList();
        return s;
    }

    public void Save() => SettingsFile.Save(this);

    /// <summary>"C:\...\OBS64.EXE" → "obs64"</summary>
    public static string Normalize(string name)
    {
        name = Path.GetFileName(name.Trim());
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
        return name.ToLowerInvariant();
    }
}
