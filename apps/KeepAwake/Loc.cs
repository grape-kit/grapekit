namespace KeepAwake;

/// <summary>Arayüz metinleri: 0 = Türkçe, 1 = English.</summary>
internal static class L
{
    public static int Lang { get => GrapekitApp.Lang; set => GrapekitApp.Lang = Math.Clamp(value, 0, 1); }

    static readonly Dictionary<string, string[]> M = new()
    {
        // Durum
        ["awake"] = ["Uyanık", "Awake"],
        ["off"] = ["Kapalı", "Off"],
        ["auto"] = ["Otomatik", "Automatic"],
        ["paused"] = ["Beklemede", "Paused"],
        ["indefinite"] = ["Süresiz", "No limit"],
        ["left"] = ["kaldı · {0}'de biter", "left · ends at {0}"],
        ["until_off"] = ["Sen kapatana kadar açık", "On until you turn it off"],
        ["sleep_normal"] = ["Bilgisayar normal şekilde uyuyabilir", "Your PC can sleep as usual"],
        ["because_proc"] = ["{0} açık olduğu için", "Because {0} is running"],
        ["because_fs"] = ["Tam ekran uygulama açık olduğu için", "Because a full-screen app is open"],
        ["paused_ac"] = ["Pilde çalıştığı için duraklatıldı", "Paused because you're on battery"],
        ["paused_low"] = ["Pil azaldığı için duraklatıldı", "Paused because the battery is low"],
        ["hero_awake_time"] = ["Uyanık · {0} kaldı", "Awake · {0} left"],
        ["hero_awake_inf"] = ["Uyanık · süresiz", "Awake · no limit"],
        ["hero_auto"] = ["Otomatik olarak uyanık", "Awake automatically"],
        ["hero_paused"] = ["Beklemede", "Paused"],
        ["hero_off"] = ["Şu an kapalı", "Currently off"],
        ["sub_screen"] = ["Bilgisayar ve ekran açık tutuluyor", "Keeping your PC and screen on"],
        ["sub_system"] = ["Bilgisayar uyanık tutuluyor, ekran kapanabilir", "Keeping your PC awake, the screen may turn off"],
        ["start"] = ["Başlat", "Start"],
        ["stop"] = ["Durdur", "Stop"],
        ["toggle_name"] = ["Uyanık tutmayı aç veya kapat", "Turn keep awake on or off"],
        ["min"] = ["{0} dk", "{0} min"],
        ["hour"] = ["{0} sa", "{0} h"],
        ["hour_min"] = ["{0} sa {1} dk", "{0} h {1} min"],
        ["d15"] = ["15 dk", "15 min"],
        ["d30"] = ["30 dk", "30 min"],
        ["d60"] = ["1 sa", "1 h"],
        ["d120"] = ["2 sa", "2 h"],
        ["d0"] = ["Süresiz", "No limit"],
        ["d_custom"] = ["Özel", "Custom"],
        ["custom_len"] = ["Özel süre", "Custom length"],
        ["custom_len_desc"] = ["Saat, dakika ve saniyeyi istediğin gibi yaz", "Type any hours, minutes and seconds"],
        ["sec"] = ["{0} sn", "{0} s"],
        ["unit_h"] = ["sa", "h"],
        ["unit_m"] = ["dk", "min"],
        ["unit_s"] = ["sn", "s"],
        ["end_action"] = ["Süre bitince", "When time is up"],
        ["end_action_desc"] = ["Süreli oturum bitince yapılır. Öncesinde 60 saniyelik bir uyarı çıkar, istersen vazgeçersin.", "Runs when a timed session ends. You get a 60-second warning first and can cancel."],
        ["end_action_off"] = ["Süresiz seçiliyken kullanılmaz", "Not used with No limit"],
        ["end0"] = ["Hiçbir şey", "Do nothing"],
        ["end1"] = ["Ekranı kapat", "Turn off screen"],
        ["end2"] = ["Kilitle", "Lock"],
        ["end3"] = ["Uykuya al", "Sleep"],
        ["end4"] = ["Bilgisayarı kapat", "Shut down"],
        ["end_verb1"] = ["ekran kapanır", "the screen turns off"],
        ["end_verb2"] = ["kilitlenir", "it locks"],
        ["end_verb3"] = ["uykuya alınır", "it goes to sleep"],
        ["end_verb4"] = ["bilgisayar kapanır", "it shuts down"],
        ["left_action"] = ["kaldı · {0}'de {1}", "left · at {0} {1}"],
        ["warn_title1"] = ["Ekran kapatılacak", "The screen will turn off"],
        ["warn_title2"] = ["Bilgisayar kilitlenecek", "Your PC will lock"],
        ["warn_title3"] = ["Bilgisayar uykuya alınacak", "Your PC will go to sleep"],
        ["warn_title4"] = ["Bilgisayar kapatılacak", "Your PC will shut down"],
        ["warn_text"] = ["Süre doldu. {0} saniye sonra yapılacak.", "Time's up. This happens in {0} seconds."],
        ["warn_now"] = ["Şimdi", "Now"],
        ["warn_cancel"] = ["Vazgeç", "Cancel"],
        ["d60_long"] = ["1 saat", "1 hour"],
        ["d120_long"] = ["2 saat", "2 hours"],

        // Panel
        ["duration"] = ["Süre", "Duration"],
        ["keep_screen"] = ["Ekranı da açık tut", "Keep the screen on too"],
        ["keep_screen_desc"] = ["Ekran kararmaz, kilitlenmez", "The screen won't dim or lock"],
        ["only_ac"] = ["Yalnızca şarjdayken", "Only while plugged in"],
        ["only_ac_desc"] = ["Pile geçince kendiliğinden durur", "Pauses by itself on battery"],

        // Sekmeler
        ["tab_general"] = ["Genel", "General"],
        ["tab_rules"] = ["Kurallar", "Rules"],
        ["tab_hotkey"] = ["Kısayol", "Shortcut"],

        // Genel
        ["how"] = ["Nasıl uyanık tutulsun?", "How should it stay awake?"],
        ["mode_system"] = ["Sistem uyanık", "System awake"],
        ["mode_system_desc"] = ["Bilgisayar uyumaz; ekran yine kapanabilir. İndirme ve yedekleme için.", "Your PC won't sleep; the screen can still turn off. For downloads and backups."],
        ["mode_screen"] = ["Sistem ve ekran", "System and screen"],
        ["mode_screen_desc"] = ["Ekran da açık kalır, kilitlenmez. Sunum, okuma ve izleme için.", "The screen stays on and won't lock. For presentations, reading and video."],
        ["default_duration"] = ["Varsayılan süre", "Default duration"],
        ["default_duration_desc"] = ["Başlat'a bastığında bu süre kadar açık kalır.", "How long it stays on when you press Start."],
        ["startup"] = ["Başlangıç", "Startup"],
        ["autostart"] = ["Windows açılınca başlat", "Start with Windows"],
        ["autostart_desc"] = ["Tepside sessizce bekler", "Waits quietly in the tray"],
        ["autostart_blocked"] = ["Windows ayarlarından kapatılmış: Ayarlar › Uygulamalar › Başlangıç", "Turned off in Windows: Settings › Apps › Startup"],
        ["start_active"] = ["Açılışta hemen etkinleştir", "Turn on at startup"],
        ["start_active_desc"] = ["Program başlar başlamaz uyanık tutmaya geçer", "Starts keeping your PC awake right away"],
        ["notify_end"] = ["Süre bitince haber ver", "Notify me when time is up"],
        ["notify_end_desc"] = ["Kısa bir Windows bildirimi gösterir", "Shows a short Windows notification"],
        ["appearance"] = ["Görünüm", "Appearance"],
        ["theme_system"] = ["Sistem", "System"],
        ["theme_light"] = ["Açık", "Light"],
        ["theme_dark"] = ["Koyu", "Dark"],
        ["language"] = ["Dil", "Language"],

        // Kurallar
        ["rule_procs"] = ["Bu programlar açıkken uyanık tut", "Stay awake while these apps are running"],
        ["rule_procs_desc"] = ["Listedeki bir program çalıştığı sürece kendiliğinden etkinleşir, kapanınca durur.", "Turns on by itself while any of these apps is running and stops when they close."],
        ["add_app"] = ["+ Program ekle", "+ Add app"],
        ["browse"] = ["Gözat…", "Browse…"],
        ["running_apps"] = ["Açık programlar", "Running apps"],
        ["remove"] = ["Kaldır", "Remove"],
        ["no_procs"] = ["Henüz program eklenmedi.", "No apps added yet."],
        ["power"] = ["Güç ve pil", "Power and battery"],
        ["low_battery"] = ["Pil %{0} altına inerse duraklat", "Pause when battery drops below {0}%"],
        ["low_battery_desc"] = ["Pilin tükenmesini önler", "Keeps your battery from running out"],
        ["fullscreen"] = ["Tam ekran uygulamalarda etkinleştir", "Turn on for full-screen apps"],
        ["fullscreen_desc"] = ["Oyun, film ve sunumlar sırasında", "During games, movies and presentations"],

        // Kısayol
        ["hotkey_title"] = ["Her yerden aç / kapat", "Toggle from anywhere"],
        ["hotkey_desc"] = ["Hangi programda olursan ol, bu tuşlarla uyanık tutmayı açıp kapatırsın.", "Turn keep awake on or off with these keys, whatever app you're in."],
        ["hotkey_change"] = ["Kısayolu değiştir", "Change shortcut"],
        ["hotkey_prompt"] = ["Yeni tuşlara bas… (Esc: vazgeç)", "Press the new keys… (Esc: cancel)"],
        ["hotkey_enabled"] = ["Kısayol etkin", "Shortcut on"],
        ["hotkey_enabled_desc"] = ["Kapatırsan tuşlar başka programlara bırakılır", "When off, the keys are left to other apps"],
        ["hotkey_busy"] = ["Bu kısayolu başka bir program kullanıyor. Farklı bir bileşim seç.", "Another app already uses this shortcut. Please pick a different one."],
        ["hotkey_hint"] = ["En az bir Ctrl, Alt veya Win tuşu içermeli. Silmek için Backspace.", "Must include Ctrl, Alt or Win. Press Backspace to clear."],

        // Tepsi
        ["tray_off"] = ["{0} — kapalı", "{0} — off"],
        ["tray_on"] = ["{0} — {1}", "{0} — {1}"],
        ["ended_title"] = ["Süre doldu", "Time's up"],
        ["ended_text"] = ["Bilgisayar artık normal şekilde uyuyabilir.", "Your PC can now sleep as usual."],
    };

    public static string T(string key) => M.TryGetValue(key, out var v) ? v[Math.Clamp(Lang, 0, 1)] : CoreText.T(key);

    /// <summary>Kalan süre: "45 sn", "36 dk", "1 sa 24 dk".</summary>
    public static string Span(TimeSpan t)
    {
        if (t.TotalSeconds < 60) return string.Format(T("sec"), Math.Max(1, (int)Math.Ceiling(t.TotalSeconds)));
        int mins = (int)Math.Ceiling(t.TotalMinutes);
        if (mins < 60) return string.Format(T("min"), Math.Max(1, mins));
        return mins % 60 == 0 ? string.Format(T("hour"), mins / 60) : string.Format(T("hour_min"), mins / 60, mins % 60);
    }

    public static readonly int[] Durations = [15, 30, 60, 120, 0];
    /// <summary>Hazır süreler + sonda "Özel".</summary>
    public static string[] DurationChips => [.. Durations.Select(d => T("d" + d)), T("d_custom")];
    public static string[] DurationLong => [.. Durations.Select(d => d >= 60 ? T($"d{d}_long") : T("d" + d)), T("d_custom")];
    public static string[] EndActions => Enumerable.Range(0, 5).Select(i => T("end" + i)).ToArray();

    /// <summary>Çip sırası: hazır süre konumu, özelse son çip.</summary>
    public static int ChipIndex(Settings s) => s.Custom ? Durations.Length : Math.Max(0, Array.IndexOf(Durations, s.Minutes));


}
