using System.Runtime.InteropServices;

namespace CapsGosterge;

internal static class Native
{
    // ── Klavye
    public const int VK_CAPITAL = 0x14, VK_NUMLOCK = 0x90, VK_SCROLL = 0x91;

    [DllImport("user32.dll")] public static extern short GetKeyState(int nVirtKey);

    public static bool IsToggled(int vk) => (GetKeyState(vk) & 1) != 0;

    public const int WH_KEYBOARD_LL = 13;
    public const int WM_KEYDOWN = 0x100, WM_KEYUP = 0x101, WM_SYSKEYDOWN = 0x104, WM_SYSKEYUP = 0x105;
    public const int LLKHF_INJECTED = 0x10;

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT { public int vkCode, scanCode, flags, time; public IntPtr dwExtraInfo; }

    public delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr GetModuleHandle(string? name);

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT { public int type; public INPUTUNION u; }
    [StructLayout(LayoutKind.Explicit)]
    public struct INPUTUNION { [FieldOffset(0)] public KEYBDINPUT ki; [FieldOffset(0)] public MOUSEINPUT mi; }
    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }
    public const int INPUT_KEYBOARD = 1;
    public const uint KEYEVENTF_KEYUP = 2;

    [DllImport("user32.dll", SetLastError = true)] public static extern uint SendInput(uint n, INPUT[] inputs, int size);

    // ── Mesaj döngüsü (ayrı iş parçacıkları için)
    [StructLayout(LayoutKind.Sequential)]
    public struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public POINT pt; }
    [DllImport("user32.dll")] public static extern int GetMessage(out MSG msg, IntPtr hWnd, uint min, uint max);
    [DllImport("user32.dll")] public static extern bool TranslateMessage(ref MSG msg);
    [DllImport("user32.dll")] public static extern IntPtr DispatchMessage(ref MSG msg);
    [DllImport("user32.dll")] public static extern bool PostThreadMessage(uint threadId, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern IntPtr SetTimer(IntPtr hWnd, IntPtr id, uint ms, IntPtr func);
    [DllImport("user32.dll")] public static extern bool KillTimer(IntPtr hWnd, IntPtr id);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    public const uint WM_TIMER = 0x113, WM_QUIT = 0x12, WM_APP = 0x8000;

    // ── Odak izleme (şifre alanı)
    public delegate void WinEventProc(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);
    [DllImport("user32.dll")]
    public static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr hmod, WinEventProc proc, uint pid, uint tid, uint flags);
    [DllImport("user32.dll")] public static extern bool UnhookWinEvent(IntPtr hook);
    public const uint EVENT_OBJECT_FOCUS = 0x8005, EVENT_SYSTEM_FOREGROUND = 0x0003;
    public const uint WINEVENT_OUTOFCONTEXT = 0, WINEVENT_SKIPOWNPROCESS = 2;

    [DllImport("oleacc.dll")]
    public static extern int AccessibleObjectFromEvent(IntPtr hwnd, int dwId, int dwChildId,
        [MarshalAs(UnmanagedType.Interface)] out Accessibility.IAccessible? acc, out object? child);

    public const int STATE_SYSTEM_PROTECTED = 0x20000000;

    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hWnd, int index);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder sb, int max);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    public const int GWL_STYLE = -16, ES_PASSWORD = 0x20;
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }

    // ── Katmanlı pencereler
    public const int WS_EX_LAYERED = 0x80000, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80,
        WS_EX_NOACTIVATE = 0x8000000, WS_EX_TOPMOST = 0x8;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; public POINT(int x, int y) { X = x; Y = y; } }
    [StructLayout(LayoutKind.Sequential)]
    public struct SIZE { public int CX, CY; public SIZE(int cx, int cy) { CX = cx; CY = cy; } }
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
    public const int ULW_ALPHA = 2;
    public const byte AC_SRC_OVER = 0, AC_SRC_ALPHA = 1;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
        IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
    [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr hDC);
    [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr hObject);
    [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr hIcon);
    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOACTIVATE = 0x10, SWP_SHOWWINDOW = 0x40;
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    public const int SW_HIDE = 0;
    [DllImport("user32.dll")] public static extern bool AllowSetForegroundWindow(int pid);

    [DllImport("user32.dll")] public static extern IntPtr MonitorFromPoint(POINT pt, uint flags);
    [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(IntPtr hmon, int type, out uint dpiX, out uint dpiY);

    public static float ScaleAt(Point p)
    {
        try
        {
            var mon = MonitorFromPoint(new POINT(p.X, p.Y), 2);
            if (GetDpiForMonitor(mon, 0, out uint dx, out _) == 0) return dx / 96f;
        }
        catch { }
        return 1f;
    }

    // ── Pencere teması
    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    public const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19, DWMWA_USE_IMMERSIVE_DARK_MODE = 20,
        DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWA_CAPTION_COLOR = 35;

    // ── Kısayol tuşu
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    public const int WM_HOTKEY = 0x312;
    public const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_SHIFT = 4, MOD_WIN = 8, MOD_NOREPEAT = 0x4000;

    // ── Tam ekran / sunum durumu
    [DllImport("shell32.dll")] public static extern int SHQueryUserNotificationState(out int state);
    public static bool IsBusyFullscreen()
    {
        try
        {
            // 2 = tam ekran uygulama, 3 = tam ekran D3D (oyun), 4 = sunum modu
            return SHQueryUserNotificationState(out int s) == 0 && s is 2 or 3 or 4;
        }
        catch { return false; }
    }

    // ── Paket (MSIX / Microsoft Store) algılama
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern int GetCurrentPackageFullName(ref int length, System.Text.StringBuilder? name);

    public static bool IsPackaged()
    {
        try
        {
            int len = 0;
            return GetCurrentPackageFullName(ref len, null) != 15700; // APPMODEL_ERROR_NO_PACKAGE
        }
        catch { return false; }
    }

    // ── Güç tasarrufu
    [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32.dll")] static extern IntPtr GetCurrentThread();
    [DllImport("kernel32.dll")] static extern bool SetProcessInformation(IntPtr h, int cls, ref POWER_THROTTLING s, int size);
    [DllImport("kernel32.dll")] static extern bool SetThreadInformation(IntPtr h, int cls, ref POWER_THROTTLING s, int size);
    [DllImport("kernel32.dll")] static extern bool SetPriorityClass(IntPtr h, uint cls);
    [DllImport("kernel32.dll")] static extern bool SetProcessWorkingSetSize(IntPtr h, IntPtr min, IntPtr max);
    [StructLayout(LayoutKind.Sequential)] struct POWER_THROTTLING { public uint Version, ControlMask, StateMask; }
    const int ProcessPowerThrottling = 4, ThreadPowerThrottling = 3;
    const uint EXECUTION_SPEED = 1, IGNORE_TIMER_RESOLUTION = 4;

    /// <summary>Windows 11 "Verimlilik modu" (EcoQoS): süreç düşük güçlü çekirdeklerde, düşük frekansta çalışır.</summary>
    public static void SetEfficiencyMode(bool on)
    {
        try
        {
            var s = new POWER_THROTTLING
            {
                Version = 1,
                ControlMask = EXECUTION_SPEED | IGNORE_TIMER_RESOLUTION,
                StateMask = on ? EXECUTION_SPEED | IGNORE_TIMER_RESOLUTION : 0,
            };
            // Not: öncelik sınıfı bilerek düşürülmez; klavye kancası yoğun yükte bile anında yanıt vermeli.
            SetProcessInformation(GetCurrentProcess(), ProcessPowerThrottling, ref s, Marshal.SizeOf<POWER_THROTTLING>());
        }
        catch { }
    }

    /// <summary>Klavye kancası iş parçacığı kısılmasın: yazarken hiçbir gecikme olmamalı.</summary>
    public static void ExemptCurrentThreadFromThrottling()
    {
        try
        {
            var s = new POWER_THROTTLING { Version = 1, ControlMask = EXECUTION_SPEED, StateMask = 0 };
            SetThreadInformation(GetCurrentThread(), ThreadPowerThrottling, ref s, Marshal.SizeOf<POWER_THROTTLING>());
        }
        catch { }
    }

    /// <summary>Ayarlar penceresi kapanınca kullanılmayan belleği işletim sistemine geri ver.</summary>
    public static void TrimWorkingSet()
    {
        try
        {
            GC.Collect(2, GCCollectionMode.Optimized, false);
            SetProcessWorkingSetSize(GetCurrentProcess(), new IntPtr(-1), new IntPtr(-1));
        }
        catch { }
    }
}
