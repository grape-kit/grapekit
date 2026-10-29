using System.Runtime.InteropServices;
using System.Text;

namespace MarkdownEditor;

internal static class Native
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern void SHAddToRecentDocs(uint flags, string path);
    [DllImport("shell32.dll")] public static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int cmd);
    [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern int GetSystemMetricsForDpi(int index, uint dpi);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool AllowSetForegroundWindow(int pid);

    public const int SM_CXFRAME = 32, SM_CYFRAME = 33, SM_CXPADDEDBORDER = 92;

    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct NCCALCSIZE_PARAMS { public RECT R0, R1, R2; public IntPtr Pos; }

    public static void AddToRecentDocs(string path) { try { SHAddToRecentDocs(3 /* SHARD_PATHW */, path); } catch { } }

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

/// <summary>Metin dosyası okuma/yazma: kodlama algılama ve koruma.</summary>
internal static class TextFiles
{
    public const long MaxBytes = 50L * 1024 * 1024;
    static TextFiles() { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); }

    public sealed record Result(string Text, string Encoding, bool Bom, string Eol, long Mtime, long Size);

    public static Result Read(string path)
    {
        var fi = new FileInfo(path);
        if (!fi.Exists) throw new FileNotFoundException(path);
        if (fi.Length > MaxBytes) throw new BridgeException("too-big", "Dosya çok büyük");
        var bytes = File.ReadAllBytes(path);
        string enc; bool bom = false; string text;
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) { enc = "utf-8"; bom = true; text = new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3); }
        else if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) { enc = "utf-16le"; bom = true; text = Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2); }
        else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) { enc = "utf-16be"; bom = true; text = Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2); }
        else if (IsValidUtf8(bytes)) { enc = "utf-8"; text = new UTF8Encoding(false).GetString(bytes); }
        else { enc = "windows-1254"; text = Encoding.GetEncoding(1254).GetString(bytes); }
        int crlf = 0, lf = 0;
        for (int i = 0; i < text.Length && i < 200_000; i++) if (text[i] == '\n') { if (i > 0 && text[i - 1] == '\r') crlf++; else lf++; }
        string eol = crlf >= lf && crlf > 0 ? "\r\n" : lf > 0 ? "\n" : "\r\n";
        return new Result(text, enc, bom, eol, Mtime(path), fi.Length);
    }

    public static long Write(string path, string text, string encoding, bool bom)
    {
        Encoding e = encoding switch
        {
            "utf-16le" => new UnicodeEncoding(false, bom),
            "utf-16be" => new UnicodeEncoding(true, bom),
            "windows-1254" => Encoding.GetEncoding(1254),
            "utf-8-bom" => new UTF8Encoding(true),
            _ => new UTF8Encoding(bom),
        };
        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);
        // Önce geçici dosyaya yaz, sonra değiştir: yazma yarıda kalırsa asıl dosya bozulmaz
        var tmp = Path.Combine(dir, "." + Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp");
        var preamble = e.GetPreamble();
        using (var fs = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            if (preamble.Length > 0) fs.Write(preamble);
            var data = e.GetBytes(text);
            fs.Write(data);
            fs.Flush(true);
        }
        try
        {
            if (File.Exists(path))
            {
                var attrs = File.GetAttributes(path);
                if ((attrs & FileAttributes.ReadOnly) != 0) throw new UnauthorizedAccessException("Dosya salt okunur");
                File.Replace(tmp, path, null, ignoreMetadataErrors: true);
            }
            else File.Move(tmp, path);
        }
        catch
        {
            try { File.Delete(tmp); } catch { }
            throw;
        }
        return Mtime(path);
    }

    public static long Mtime(string path) => new DateTimeOffset(File.GetLastWriteTimeUtc(path)).ToUnixTimeMilliseconds();

    static bool IsValidUtf8(byte[] b)
    {
        try { _ = new UTF8Encoding(false, true).GetString(b); return true; } catch { return false; }
    }
}

internal sealed class BridgeException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
