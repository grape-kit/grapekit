using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace KeepAwake;

internal enum Reason { None, Manual, Process, Fullscreen }

/// <summary>
/// Uyanık tutma motoru. Elle başlatma (süreli/süresiz) ve kurallar (program açık, tam ekran) birleşir;
/// güç kuralları (yalnızca şarjda, düşük pil) hepsini duraklatabilir.
/// Windows'a SetThreadExecutionState ile bildirir: hiçbir ayar kalıcı olarak değiştirilmez, program kapanınca biter.
/// </summary>
internal sealed class AwakeEngine : IDisposable
{
    const uint ES_CONTINUOUS = 0x80000000, ES_SYSTEM_REQUIRED = 0x1, ES_DISPLAY_REQUIRED = 0x2;
    [DllImport("kernel32.dll")] static extern uint SetThreadExecutionState(uint flags);
    [DllImport("shell32.dll")] static extern int SHQueryUserNotificationState(out int state);

    readonly Settings _s;
    readonly System.Windows.Forms.Timer _tick = new() { Interval = 1000 };
    int _ruleCountdown;
    uint _applied;
    string? _process;
    bool _fullscreen;

    public bool ManualOn { get; private set; }
    /// <summary>Elle başlatmanın bitiş anı (UTC); null = süresiz.</summary>
    public DateTime? Until { get; private set; }
    public int SessionSeconds { get; private set; }
    public Reason Reason { get; private set; }
    public string? ProcessName => _process;
    /// <summary>Güç kuralı yüzünden duraklatıldı (true: pilde, false + LowBattery: pil düşük).</summary>
    public bool Paused { get; private set; }
    public bool PausedForLowBattery { get; private set; }
    public bool Awake => Reason != Reason.None && !Paused;

    /// <summary>Durum değişti (açıldı, kapandı, sebep değişti).</summary>
    public event Action? Changed;
    /// <summary>Her saniye (geri sayımı göstermek için).</summary>
    public event Action? Tick;
    /// <summary>Elle başlatılan süre doldu.</summary>
    public event Action? Expired;

    public AwakeEngine(Settings s)
    {
        _s = s;
        _tick.Tick += (_, _) => OnTick();
        _tick.Start();
        SystemEvents.PowerModeChanged += OnPower;
        Evaluate(force: true);
    }

    public TimeSpan Remaining => ManualOn && Until is { } u ? (u > DateTime.UtcNow ? u - DateTime.UtcNow : TimeSpan.Zero) : TimeSpan.Zero;

    /// <summary>Halka için doluluk: süreli oturumda kalan oran, aksi halde açıkken 1.</summary>
    public double Fraction => !Awake ? 0 : ManualOn && SessionSeconds > 0 ? Remaining.TotalSeconds / SessionSeconds : 1;

    /// <param name="seconds">0 = süresiz</param>
    public void Start(int seconds)
    {
        ManualOn = true;
        SessionSeconds = seconds;
        Until = seconds > 0 ? DateTime.UtcNow.AddSeconds(seconds) : null;
        Evaluate(force: true);
    }

    public void Stop()
    {
        ManualOn = false;
        Until = null;
        Evaluate(force: true);
    }

    public Settings Settings => _s;

    public void Toggle() { if (ManualOn) Stop(); else Start(_s.EffectiveSeconds); }

    /// <summary>Hazır süre seçildi: açıksa bu süreyle yeniden başlar.</summary>
    public void SetDuration(int minutes)
    {
        _s.Minutes = minutes;
        _s.Custom = false;
        if (ManualOn) Start(minutes * 60);
        else Changed?.Invoke();
    }

    /// <summary>Özel süre seçildi/değişti (saniye): açıksa bu süreyle yeniden başlar.</summary>
    public void SetCustom(int seconds)
    {
        _s.Custom = true;
        _s.CustomSeconds = Math.Max(1, seconds);
        if (ManualOn) Start(_s.CustomSeconds);
        else Changed?.Invoke();
    }

    void OnTick()
    {
        if (ManualOn && Until is { } u && DateTime.UtcNow >= u)
        {
            ManualOn = false;
            Until = null;
            Evaluate(force: true);
            Expired?.Invoke();
            return;
        }
        if (--_ruleCountdown <= 0) Evaluate(force: false);
        Tick?.Invoke();
    }

    void OnPower(object? sender, PowerModeChangedEventArgs e)
    {
        // SystemEvents ayrı bir iş parçacığından gelebilir; zamanlayıcı bir sonraki saniyede değerlendirir.
        _ruleCountdown = 0;
    }

    /// <summary>Kuralları yeniden değerlendirir (ayar değişince de çağrılır).</summary>
    public void Evaluate(bool force)
    {
        _ruleCountdown = 5;
        _process = _s.Processes.Count > 0 ? FindProcess() : null;
        _fullscreen = _s.Fullscreen && IsFullscreenBusy();

        var reason = ManualOn ? Reason.Manual : _process != null ? Reason.Process : _fullscreen ? Reason.Fullscreen : Reason.None;

        var ps = SystemInformation.PowerStatus;
        bool hasBattery = (ps.BatteryChargeStatus & BatteryChargeStatus.NoSystemBattery) == 0 && ps.BatteryChargeStatus != BatteryChargeStatus.Unknown;
        bool onBattery = hasBattery && ps.PowerLineStatus == PowerLineStatus.Offline;
        int pct = (int)Math.Round(ps.BatteryLifePercent * 100);
        bool low = onBattery && _s.PauseOnLowBattery && pct < _s.LowBatteryPercent;
        bool paused = reason != Reason.None && ((_s.OnlyOnAC && onBattery) || low);

        bool changed = reason != Reason || paused != Paused || low != PausedForLowBattery;
        Reason = reason; Paused = paused; PausedForLowBattery = paused && low;
        Apply();
        if (changed || force) Changed?.Invoke();
    }

    /// <summary>Mod (ekran açık/kapalı) değişince Windows'a yeniden bildirir.</summary>
    public void Apply()
    {
        uint flags = ES_CONTINUOUS;
        if (Awake) flags |= ES_SYSTEM_REQUIRED | (_s.Mode == 1 ? ES_DISPLAY_REQUIRED : 0);
        if (flags == _applied) return;
        SetThreadExecutionState(flags);
        _applied = flags;
    }

    string? FindProcess()
    {
        Process[] all;
        try { all = Process.GetProcesses(); } catch { return null; }
        string? found = null;
        foreach (var p in all)
        {
            if (found == null)
            {
                try
                {
                    var n = p.ProcessName.ToLowerInvariant();
                    if (_s.Processes.Contains(n)) found = p.ProcessName;
                }
                catch { }
            }
            p.Dispose();
        }
        return found;
    }

    static bool IsFullscreenBusy()
    {
        try
        {
            // 2 = tam ekran uygulama, 3 = Direct3D tam ekran, 4 = sunum modu
            return SHQueryUserNotificationState(out int st) == 0 && st is 2 or 3 or 4;
        }
        catch { return false; }
    }

    public void Dispose()
    {
        SystemEvents.PowerModeChanged -= OnPower;
        _tick.Dispose();
        SetThreadExecutionState(ES_CONTINUOUS);
    }
}
