using System.Text.Json;
using Microsoft.Win32;

namespace Grapekit;

/// <summary>
/// Ayar dosyası: %APPDATA%\&lt;Exe&gt;\settings.json.
/// GRAPEKIT_SETTINGS_DIR: ekran görüntüsü ve testler için ayrı klasör (kullanıcının ayarlarına dokunmadan).
/// </summary>
public static class SettingsFile
{
    public static string Dir => Environment.GetEnvironmentVariable("GRAPEKIT_SETTINGS_DIR") is { Length: > 0 } d
        ? d : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), GrapekitApp.Exe);
    static string FilePath => Path.Combine(Dir, "settings.json");
    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static bool Exists => File.Exists(FilePath);

    public static T Load<T>() where T : new()
    {
        try { return File.Exists(FilePath) ? JsonSerializer.Deserialize<T>(File.ReadAllText(FilePath)) ?? new T() : new T(); }
        catch { return new T(); }
    }

    public static void Save<T>(T value)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(value, Options));
            File.Move(tmp, FilePath, overwrite: true);
        }
        catch { }
    }
}

/// <summary>Beklenmedik hataları ayar klasöründeki hata.log dosyasına yazar.</summary>
public static class ErrorLog
{
    public static void Write(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(SettingsFile.Dir);
            var path = Path.Combine(SettingsFile.Dir, "hata.log");
            if (File.Exists(path) && new FileInfo(path).Length > 512_000) File.Delete(path);
            File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] v{GrapekitApp.VersionText} {ex}\n\n");
        }
        catch { }
    }

    /// <summary>Program.Main'de: yakalanmamış hatalar programı düşürmesin, kayda geçsin.</summary>
    public static void Install()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Write(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => { if (e.ExceptionObject is Exception ex) Write(ex); };
        TaskScheduler.UnobservedTaskException += (_, e) => { Write(e.Exception); e.SetObserved(); };
    }
}

/// <summary>
/// Windows açılışında çalıştırma.
/// - setup.exe ile kurulumda: HKCU\...\Run kaydı (ad: Exe).
/// - Microsoft Store (MSIX) kurulumunda: paket bildirimindeki StartupTask (kimlik: Exe + "Startup").
/// </summary>
public static class AutoStart
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    static string Name => GrapekitApp.Exe;
    public static string TaskId => GrapekitApp.Exe + "Startup";

    /// <summary>Sonuç: true = etkin, false = kapalı, null = kullanıcı Windows ayarlarından kapatmış (uygulama açamaz).</summary>
    public static async Task<bool?> ApplyAsync(bool enabled)
    {
        if (GrapekitApp.IsPackaged) return await ApplyPackagedAsync(enabled);
        try
        {
            using var run = Registry.CurrentUser.CreateSubKey(RunKey, true);
            if (enabled)
            {
                run.SetValue(Name, $"\"{Environment.ProcessPath}\" --autostart");
                using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey, true);
                approved?.DeleteValue(Name, false);
            }
            else run.DeleteValue(Name, false);
        }
        catch { }
        return enabled;
    }

    static async Task<bool?> ApplyPackagedAsync(bool enabled)
    {
        try
        {
            var task = await Windows.ApplicationModel.StartupTask.GetAsync(TaskId);
            switch (task.State)
            {
                case Windows.ApplicationModel.StartupTaskState.Enabled when !enabled:
                    task.Disable();
                    return false;
                case Windows.ApplicationModel.StartupTaskState.Disabled when enabled:
                    var st = await task.RequestEnableAsync();
                    return st is Windows.ApplicationModel.StartupTaskState.Enabled or Windows.ApplicationModel.StartupTaskState.EnabledByPolicy;
                case Windows.ApplicationModel.StartupTaskState.DisabledByUser:
                case Windows.ApplicationModel.StartupTaskState.DisabledByPolicy:
                    return enabled ? null : false;
                default:
                    return task.State is Windows.ApplicationModel.StartupTaskState.Enabled or Windows.ApplicationModel.StartupTaskState.EnabledByPolicy;
            }
        }
        catch { return false; }
    }

    /// <summary>Paketli uygulama başlangıç görevinden mi açıldı?</summary>
    public static bool LaunchedByStartupTask()
    {
        if (!GrapekitApp.IsPackaged) return false;
        try
        {
            var args = Windows.ApplicationModel.AppInstance.GetActivatedEventArgs();
            return args?.Kind == Windows.ApplicationModel.Activation.ActivationKind.StartupTask;
        }
        catch { return false; }
    }
}

/// <summary>
/// Güncelleme denetimi ve kurulumu (yalnızca setup.exe sürümü): günde bir otomatik denetim, elle denetim,
/// indirip kurma. Durum metni ayarlar penceresinin Hakkında sekmesinde gösterilir.
/// </summary>
public sealed class UpdateManager : IDisposable
{
    readonly Func<DateTime> _getLast;
    readonly Action<DateTime> _setLast;
    readonly Func<bool> _auto;
    readonly System.Windows.Forms.Timer _timer = new() { Interval = 3 * 60 * 60 * 1000 };
    readonly System.Windows.Forms.Timer _first = new() { Interval = 90 * 1000 };
    string _status = "";

    public Updater.Info? Available { get; private set; }
    public bool Busy { get; private set; }
    /// <summary>Durum değişti (denetim başladı/bitti, yeni sürüm bulundu): düğmeler yeniden kurulmalı.</summary>
    public event Action? Changed;
    /// <summary>Yalnızca durum yazısı değişti (indirme yüzdesi): yazıyı yeniden çizmek yeterli.</summary>
    public event Action? Progress;
    /// <summary>Otomatik denetimde yeni sürüm bulundu (uygulama bildirim gösterir).</summary>
    public event Action<Updater.Info>? Found;

    public UpdateManager(Func<DateTime> getLast, Action<DateTime> setLast, Func<bool> auto)
    {
        _getLast = getLast; _setLast = setLast; _auto = auto;
        if (!Updater.Supported) return;
        _timer.Tick += (_, _) => _ = AutoCheckAsync();
        _first.Tick += (_, _) => { _first.Stop(); _ = AutoCheckAsync(); };
        _timer.Start();
        _first.Start();
    }

    public string StatusText
    {
        get
        {
            if (!Updater.Supported) return CoreText.T("upd_store");
            if (_status.Length > 0) return _status;
            if (Available is { } a) return string.Format(CoreText.T("upd_found"), a.Version.ToString(3));
            var last = _getLast();
            return last == default ? CoreText.T("upd_never")
                : string.Format(CoreText.T("upd_last"), last.ToLocalTime().ToString("g", CoreText.Culture));
        }
    }

    void Set(string status, bool busy) { _status = status; Busy = busy; Changed?.Invoke(); }

    async Task AutoCheckAsync()
    {
        if (!_auto() || (DateTime.UtcNow - _getLast()).TotalHours < 20) return;
        await CheckAsync(manual: false);
    }

    public async Task CheckAsync(bool manual)
    {
        if (Busy || !Updater.Supported) return;
        Set(manual ? CoreText.T("upd_checking") : "", true);
        try
        {
            Available = await Updater.CheckAsync();
            _setLast(DateTime.UtcNow);
            Set(Available == null && manual ? CoreText.T("upd_latest") : "", false);
            if (Available != null && !manual) Found?.Invoke(Available);
        }
        catch
        {
            Set(manual ? CoreText.T("upd_error") : "", false);
        }
    }

    public async Task InstallAsync()
    {
        if (Busy || Available is not { } info) return;
        Set(string.Format(CoreText.T("upd_downloading"), 0), true);
        try
        {
            var progress = new Progress<int>(p => { _status = string.Format(CoreText.T("upd_downloading"), p); Progress?.Invoke(); });
            var path = await Updater.DownloadAsync(info, progress);
            Updater.RunInstaller(path);
            Application.Exit();
        }
        catch (Exception ex)
        {
            Set(string.Format(CoreText.T("upd_failed"), ex.Message), false);
        }
    }

    public void Dispose() { _timer.Dispose(); _first.Dispose(); }
}
