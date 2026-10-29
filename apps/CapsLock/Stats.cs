using System.Text.Json;

namespace CapsGosterge;

internal sealed class DayStats
{
    public int CapsOn { get; set; }
    public int Accidental { get; set; }
    public int Blocked { get; set; }
    public int PasswordWarnings { get; set; }
    public int NumToggles { get; set; }
}

/// <summary>
/// Günlük sayaçlar. Hangi tuşlara basıldığı ASLA kaydedilmez; yalnızca kilit tuşu sayıları tutulur.
/// Diske yazma toplu yapılır (en sık 30 sn'de bir) ki disk boşuna uyanmasın.
/// </summary>
internal sealed class Stats : IDisposable
{
    const int KeepDays = 60;
    static string FilePath => Path.Combine(AppSettings.Dir, "istatistik.json");

    readonly Dictionary<string, DayStats> _days;
    readonly System.Windows.Forms.Timer _saveTimer = new() { Interval = 30_000 };
    bool _dirty;

    public event Action? Changed;

    public Stats()
    {
        try
        {
            _days = File.Exists(FilePath)
                ? JsonSerializer.Deserialize<Dictionary<string, DayStats>>(File.ReadAllText(FilePath)) ?? []
                : [];
        }
        catch { _days = []; }
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); Flush(); };
    }

    static string Key(DateTime d) => d.ToString("yyyy-MM-dd");

    public DayStats Get(DateTime day) => _days.TryGetValue(Key(day), out var s) ? s : new DayStats();

    public void Add(Action<DayStats> change)
    {
        var key = Key(DateTime.Today);
        if (!_days.TryGetValue(key, out var s)) _days[key] = s = new DayStats();
        change(s);
        _dirty = true;
        if (!_saveTimer.Enabled) _saveTimer.Start();
        Changed?.Invoke();
    }

    public void Reset()
    {
        _days.Clear();
        _dirty = true;
        Flush();
        Changed?.Invoke();
    }

    public void Flush()
    {
        if (!_dirty) return;
        _dirty = false;
        try
        {
            var cutoff = DateTime.Today.AddDays(-KeepDays);
            foreach (var k in _days.Keys.Where(k => DateTime.TryParse(k, out var d) && d < cutoff).ToList())
                _days.Remove(k);
            Directory.CreateDirectory(AppSettings.Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(_days));
        }
        catch { }
    }

    public void Dispose()
    {
        Flush();
        _saveTimer.Dispose();
    }
}
