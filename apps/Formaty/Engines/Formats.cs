namespace Formaty.Engines;

public enum Kind { Image, Video, Audio, Pdf, Other }

/// <summary>Uzantı → tür ve hangi motorun açacağı. Yazılabilen hedef biçimler arayüzde (formats.js) tanımlıdır.</summary>
public static class Formats
{
    // libvips'in kendisinin açtıkları
    public static readonly HashSet<string> VipsImage = Set("jpg", "jpeg", "jpe", "jfif", "png", "webp", "avif", "heic", "heif", "hif", "gif", "tif", "tiff", "svg");
    // Windows'un (GDI+) açtıkları
    public static readonly HashSet<string> GdiImage = Set("bmp", "dib", "ico", "cur");
    // FFmpeg'in görsel çözücüleriyle açılanlar
    public static readonly HashSet<string> FfImage = Set("jxl", "jp2", "j2k", "jpf", "tga", "qoi", "pcx", "ppm", "pgm", "pbm", "pam", "dds", "psd", "exr", "hdr", "sgi", "xbm", "xpm", "sun", "ras", "dpx");

    public static readonly HashSet<string> Video = Set("mp4", "m4v", "mkv", "mov", "avi", "webm", "wmv", "flv", "mts", "m2ts", "ts", "3gp", "3g2", "mpg", "mpeg", "mpe", "vob", "ogv", "f4v", "asf", "divx", "rm", "rmvb", "mxf", "y4m", "dv", "hevc", "h264", "h265");
    public static readonly HashSet<string> Audio = Set("mp3", "aac", "m4a", "m4b", "wav", "flac", "ogg", "oga", "opus", "wma", "aiff", "aif", "aifc", "amr", "ac3", "eac3", "dts", "ape", "wv", "mka", "caf", "au", "snd", "mp2", "mpa", "spx", "tta", "w64");

    static HashSet<string> Set(params string[] xs) => new(xs, StringComparer.OrdinalIgnoreCase);

    public static string Ext(string path) => Path.GetExtension(path).TrimStart('.').ToLowerInvariant();

    public static Kind KindOf(string path)
    {
        var e = Ext(path);
        if (e == "pdf") return Kind.Pdf;
        if (VipsImage.Contains(e) || GdiImage.Contains(e) || FfImage.Contains(e)) return Kind.Image;
        if (Video.Contains(e)) return Kind.Video;
        if (Audio.Contains(e)) return Kind.Audio;
        return Kind.Other;
    }

    public static bool Supported(string path) => KindOf(path) != Kind.Other;

    /// <summary>Hedef biçimin dosya uzantısı (ör. "alac" → "m4a").</summary>
    public static string OutExt(string format) => format switch
    {
        "jpeg" => "jpg",
        "aac" => "m4a",
        "alac" => "m4a",
        "tiff" => "tif",
        "webp-anim" => "webp",
        _ => format,
    };
}
