using Microsoft.Win32;

namespace CapsGosterge;

/// <summary>
/// Windows açılışında çalıştırma.
/// - setup.exe ile kurulumda: HKCU\...\Run kaydı.
/// - Microsoft Store (MSIX) kurulumunda: paket bildirimindeki StartupTask (Run kaydı paket içinde çalışmaz).
/// </summary>
internal static class AutoStart
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    const string Name = "CapsGosterge";
    public const string TaskId = "CapsGostergeStartup";

    /// <summary>Sonuç: true = etkin, false = kapalı, null = kullanıcı Windows ayarlarından kapatmış (uygulama açamaz).</summary>
    public static async Task<bool?> ApplyAsync(bool enabled)
    {
        if (AppInfo.IsPackaged) return await ApplyPackagedAsync(enabled);
        ApplyRegistry(enabled);
        return enabled;
    }

    static void ApplyRegistry(bool enabled)
    {
        try
        {
            using var run = Registry.CurrentUser.CreateSubKey(RunKey, true);
            if (enabled)
            {
                run.SetValue(Name, $"\"{Environment.ProcessPath}\" --autostart");
                using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey, true);
                approved?.DeleteValue(Name, false);
            }
            else
            {
                run.DeleteValue(Name, false);
            }
        }
        catch { }
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
                    return st is Windows.ApplicationModel.StartupTaskState.Enabled
                        or Windows.ApplicationModel.StartupTaskState.EnabledByPolicy;
                case Windows.ApplicationModel.StartupTaskState.DisabledByUser:
                case Windows.ApplicationModel.StartupTaskState.DisabledByPolicy:
                    return enabled ? null : false;
                default:
                    return task.State is Windows.ApplicationModel.StartupTaskState.Enabled
                        or Windows.ApplicationModel.StartupTaskState.EnabledByPolicy;
            }
        }
        catch { return false; }
    }

    /// <summary>Paketli uygulama başlangıç görevinden mi açıldı?</summary>
    public static bool LaunchedByStartupTask()
    {
        if (!AppInfo.IsPackaged) return false;
        try
        {
            var args = Windows.ApplicationModel.AppInstance.GetActivatedEventArgs();
            return args?.Kind == Windows.ApplicationModel.Activation.ActivationKind.StartupTask;
        }
        catch { return false; }
    }
}

internal static class AppInfo
{
    public static bool IsPackaged => GrapekitApp.IsPackaged;
    public static Version Version => GrapekitApp.Version;
    public static string VersionText => GrapekitApp.VersionText;
    public static string Website => GrapekitApp.PageUrl;
    public static string PrivacyUrl => Brand.PrivacyUrl;
}
