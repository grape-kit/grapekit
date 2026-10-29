namespace CapsGosterge;

internal static class Program
{
    const string MutexName = @"Local\CapsGosterge.Tekil";
    const string ShowEventName = @"Local\CapsGosterge.Goster";

    [STAThread]
    static void Main(string[] args)
    {
        GrapekitApp.Init("caps-lock", "CapsGosterge", ["Caps Lock Göstergesi", "Caps Lock Indicator", "Feststelltasten-Anzeige"],
            "caps-lock-gostergesi", "caps-lock-indicator");
        bool autostart = args.Any(a => a.Equals("--autostart", StringComparison.OrdinalIgnoreCase))
                         || AutoStart.LaunchedByStartupTask();

        using var mutex = new Mutex(true, MutexName, out bool created);
        if (!created)
        {
            // Zaten çalışıyor: elle açıldıysa var olan kopyanın ayarlar penceresini öne getir.
            if (!autostart)
            {
                try
                {
                    Native.AllowSetForegroundWindow(-1);
                    using var ev = EventWaitHandle.OpenExisting(ShowEventName);
                    ev.Set();
                }
                catch { }
            }
            return;
        }

        using var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        ApplicationConfiguration.Initialize();
        // Beklenmedik bir hata arka plandaki programı düşürmesin; sadece kayda geçsin.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => LogError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => { if (e.ExceptionObject is Exception ex) LogError(ex); };
        TaskScheduler.UnobservedTaskException += (_, e) => { LogError(e.Exception); e.SetObserved(); };

        int page = args.Select(a => a.StartsWith("--page=") && int.TryParse(a[7..], out int n) ? n : -1).DefaultIfEmpty(-1).Max();
        using var app = new TrayApp(showSettings: !autostart, showEvent, page);
        Application.Run(app);
    }

    static void LogError(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(AppSettings.Dir);
            var path = Path.Combine(AppSettings.Dir, "hata.log");
            if (File.Exists(path) && new FileInfo(path).Length > 512_000) File.Delete(path);
            File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] v{AppInfo.VersionText} {ex}\n\n");
        }
        catch { }
    }
}
