using System.Runtime.InteropServices;

namespace Grapekit.Ui;

/// <summary>Ortak bileşenlerin ihtiyaç duyduğu Windows çağrıları.</summary>
public static class Win32
{
    [DllImport("user32.dll")] public static extern short GetKeyState(int nVirtKey);
    public const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_SHIFT = 4, MOD_WIN = 8, MOD_NOREPEAT = 0x4000;

    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    public const int WM_HOTKEY = 0x312;

    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    public const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19, DWMWA_USE_IMMERSIVE_DARK_MODE = 20, DWMWA_CAPTION_COLOR = 35;

    /// <summary>Pencere başlık çubuğunu temaya uydurur (koyu mod; Windows 11'de başlık rengi).</summary>
    public static void ThemeTitleBar(IntPtr hwnd, Palette p)
    {
        int dark = p.Dark ? 1 : 0;
        if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, 4) != 0)
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref dark, 4);
        int color = p.Bg.R | (p.Bg.G << 8) | (p.Bg.B << 16);
        DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref color, 4);
    }
}

/// <summary>Genel kısayol tuşu. Yalnızca mesaj alan görünmez bir pencere kullanır.</summary>
public sealed class GlobalHotkey : NativeWindow, IDisposable
{
    readonly int _id;
    bool _registered;
    public event Action? Pressed;

    public GlobalHotkey(int id = 0x4701)
    {
        _id = id;
        CreateHandle(new CreateParams { Parent = new IntPtr(-3) /* HWND_MESSAGE */ });
    }

    /// <returns>Kayıt başarılı mı (başka program aynı kısayolu kullanıyorsa false)</returns>
    public bool Set(bool enabled, int combo)
    {
        Clear();
        if (!enabled || (combo & 0xFFFF) == 0) return true;
        _registered = Win32.RegisterHotKey(Handle, _id, ((uint)(combo >> 16) & 0xF) | Win32.MOD_NOREPEAT, (uint)combo & 0xFFFF);
        return _registered;
    }

    void Clear()
    {
        if (_registered) Win32.UnregisterHotKey(Handle, _id);
        _registered = false;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Win32.WM_HOTKEY && m.WParam == _id) Pressed?.Invoke();
        base.WndProc(ref m);
    }

    public void Dispose() { Clear(); DestroyHandle(); }
}
