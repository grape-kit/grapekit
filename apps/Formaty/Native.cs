using System.Runtime.InteropServices;

namespace Formaty;

internal static class Native
{
    [DllImport("shell32.dll")] public static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern int GetSystemMetricsForDpi(int index, uint dpi);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool AllowSetForegroundWindow(int pid);

    public const int SM_CYFRAME = 33, SM_CXPADDEDBORDER = 92;

    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct NCCALCSIZE_PARAMS { public RECT R0, R1, R2; public IntPtr Pos; }

    // --- Geri Dönüşüm Kutusu ---
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd; public uint wFunc; public string pFrom; public string? pTo; public ushort fFlags;
        public bool fAnyOperationsAborted; public IntPtr hNameMappings; public string? lpszProgressTitle;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern int SHFileOperation(ref SHFILEOPSTRUCT op);

    public static void Recycle(string path)
    {
        var op = new SHFILEOPSTRUCT { wFunc = 3 /* FO_DELETE */, pFrom = path + "\0\0", fFlags = 0x0040 | 0x0010 | 0x0400 | 0x0004 /* ALLOWUNDO|NOCONFIRMATION|NOERRORUI|SILENT */ };
        int r = SHFileOperation(ref op);
        if (r != 0) throw new IOException($"Silinemedi (0x{r:X})");
    }
}

/// <summary>Arayüze kullanıcıya gösterilecek bir hata olarak döner (günlüğe yazılmaz).</summary>
internal sealed class BridgeException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
