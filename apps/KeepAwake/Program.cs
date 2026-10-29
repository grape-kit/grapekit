namespace KeepAwake;

internal static class Program
{
    const string MutexName = @"Local\KeepAwake.Tekil";
    const string ShowEventName = @"Local\KeepAwake.Goster";

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool AllowSetForegroundWindow(int pid);

    [STAThread]
    static void Main(string[] args)
    {
        GrapekitApp.Init("keep-awake", "KeepAwake", ["Uyku Engelleyici", "Keep Awake", "Keep Awake"], "uyku-engelleyici", "keep-awake");
        bool autostart = args.Any(a => a.Equals("--autostart", StringComparison.OrdinalIgnoreCase)) || AutoStart.LaunchedByStartupTask();

        using var mutex = new Mutex(true, MutexName, out bool created);
        if (!created)
        {
            // Zaten çalışıyor: elle açıldıysa var olan kopyanın ayarlar penceresini öne getir.
            if (!autostart)
            {
                try
                {
                    AllowSetForegroundWindow(-1);
                    using var ev = EventWaitHandle.OpenExisting(ShowEventName);
                    ev.Set();
                }
                catch { }
            }
            return;
        }

        using var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        ApplicationConfiguration.Initialize();
        ErrorLog.Install();

        int page = args.Select(a => a.StartsWith("--page=") && int.TryParse(a[7..], out int n) ? n : -1).DefaultIfEmpty(-1).Max();
        bool panel = args.Contains("--panel"); // ekran görüntüsü için: açılışta paneli göster
        using var app = new TrayApp(showSettings: !autostart && !panel, showEvent, page);
        if (panel) app.ShowPanelOnStart();
        Application.Run(app);
    }
}
