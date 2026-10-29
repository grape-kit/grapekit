using System.Runtime.InteropServices;

namespace Grapekit.Ui;

/// <summary>Ortak kontrollerin ihtiyaç duyduğu Windows çağrıları.</summary>
public static class Win32
{
    [DllImport("user32.dll")] public static extern short GetKeyState(int nVirtKey);
    public const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_SHIFT = 4, MOD_WIN = 8, MOD_NOREPEAT = 0x4000;
}
