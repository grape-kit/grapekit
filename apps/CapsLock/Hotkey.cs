namespace CapsGosterge;

/// <summary>Genel kısayol tuşu (ör. Ctrl+Alt+C). Yalnızca mesaj alan görünmez bir pencere kullanır.</summary>
internal sealed class Hotkey : NativeWindow, IDisposable
{
    const int Id = 0x4347;
    bool _registered;

    public event Action? Pressed;

    public Hotkey()
    {
        CreateHandle(new CreateParams { Parent = new IntPtr(-3) /* HWND_MESSAGE */ });
    }

    /// <returns>Kayıt başarılı mı (başka program aynı kısayolu kullanıyorsa false)</returns>
    public bool Set(bool enabled, int combo)
    {
        Clear();
        if (!enabled || (combo & 0xFFFF) == 0) return true;
        uint mods = (uint)(combo >> 16) & 0xF;
        uint vk = (uint)combo & 0xFFFF;
        _registered = Native.RegisterHotKey(Handle, Id, mods | Native.MOD_NOREPEAT, vk);
        return _registered;
    }

    void Clear()
    {
        if (_registered) Native.UnregisterHotKey(Handle, Id);
        _registered = false;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Native.WM_HOTKEY && m.WParam == Id) Pressed?.Invoke();
        base.WndProc(ref m);
    }

    public static string Describe(int combo)
    {
        if ((combo & 0xFFFF) == 0) return "—";
        uint mods = (uint)(combo >> 16);
        var parts = new List<string>();
        if ((mods & Native.MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((mods & Native.MOD_ALT) != 0) parts.Add("Alt");
        if ((mods & Native.MOD_SHIFT) != 0) parts.Add("Shift");
        if ((mods & Native.MOD_WIN) != 0) parts.Add("Win");
        var key = (Keys)(combo & 0xFFFF);
        parts.Add(key switch
        {
            >= Keys.D0 and <= Keys.D9 => ((char)('0' + (key - Keys.D0))).ToString(),
            Keys.Oemcomma => ",",
            Keys.OemPeriod => ".",
            Keys.OemMinus => "-",
            Keys.Oemplus => "+",
            _ => key.ToString(),
        });
        return string.Join(" + ", parts);
    }

    public void Dispose()
    {
        Clear();
        DestroyHandle();
    }
}
