using System.Runtime.InteropServices;

namespace Formaty.Engines;

/// <summary>
/// PDFium (engines\pdfium.dll) — yalnızca sayfaları görüntüye çevirmek için gereken birkaç işlev.
/// PDFium iş parçacığı güvenli değildir: bütün çağrılar tek kilitten geçer.
/// </summary>
public static class Pdfium
{
    const string Dll = "pdfium";
    static readonly object Lock = new();
    static bool _ready;

    static Pdfium()
    {
        NativeLibrary.SetDllImportResolver(typeof(Pdfium).Assembly, (name, asm, path) =>
            name == Dll && NativeLibrary.TryLoad(Path.Combine(AppContext.BaseDirectory, "engines", "pdfium.dll"), out var h) ? h : IntPtr.Zero);
    }

    [DllImport(Dll)] static extern void FPDF_InitLibrary();
    [DllImport(Dll, CharSet = CharSet.Ansi)] static extern IntPtr FPDF_LoadMemDocument64(IntPtr data, nuint size, string? password);
    [DllImport(Dll)] static extern void FPDF_CloseDocument(IntPtr doc);
    [DllImport(Dll)] static extern int FPDF_GetPageCount(IntPtr doc);
    [DllImport(Dll)] static extern uint FPDF_GetLastError();
    [DllImport(Dll)] static extern IntPtr FPDF_LoadPage(IntPtr doc, int index);
    [DllImport(Dll)] static extern void FPDF_ClosePage(IntPtr page);
    [DllImport(Dll)] static extern float FPDF_GetPageWidthF(IntPtr page);
    [DllImport(Dll)] static extern float FPDF_GetPageHeightF(IntPtr page);
    [DllImport(Dll)] static extern IntPtr FPDFBitmap_Create(int w, int h, int alpha);
    [DllImport(Dll)] static extern void FPDFBitmap_FillRect(IntPtr bmp, int l, int t, int w, int h, uint color);
    [DllImport(Dll)] static extern IntPtr FPDFBitmap_GetBuffer(IntPtr bmp);
    [DllImport(Dll)] static extern int FPDFBitmap_GetStride(IntPtr bmp);
    [DllImport(Dll)] static extern void FPDFBitmap_Destroy(IntPtr bmp);
    [DllImport(Dll)] static extern void FPDF_RenderPageBitmap(IntPtr bmp, IntPtr page, int x, int y, int w, int h, int rotate, int flags);

    const int FPDF_ANNOT = 0x01, FPDF_LCD_TEXT = 0x02, FPDF_PRINTING = 0x800;

    /// <summary>Açık bir PDF (bellekte). Kapatılınca bellek serbest kalır.</summary>
    public sealed class Doc : IDisposable
    {
        internal IntPtr H;
        GCHandle _pin;
        public int Pages { get; }

        internal Doc(byte[] data, string? password)
        {
            _pin = GCHandle.Alloc(data, GCHandleType.Pinned);
            lock (Lock)
            {
                if (!_ready) { FPDF_InitLibrary(); _ready = true; }
                H = FPDF_LoadMemDocument64(_pin.AddrOfPinnedObject(), (nuint)data.Length, password);
                if (H == IntPtr.Zero)
                {
                    var err = FPDF_GetLastError();
                    _pin.Free();
                    throw new InvalidOperationException(err == 4 ? Text.T("err.pdfPassword") : Text.T("err.pdfOpen"));
                }
                Pages = FPDF_GetPageCount(H);
            }
        }

        /// <summary>Sayfayı BGRA piksel olarak çizer (beyaz zemin).</summary>
        public (byte[] Bgra, int W, int H, int Stride) Render(int index, double dpi, int maxSide = 12000)
        {
            lock (Lock)
            {
                var page = FPDF_LoadPage(H, index);
                if (page == IntPtr.Zero) throw new InvalidOperationException(Text.T("err.pdfOpen"));
                try
                {
                    double s = dpi / 72.0;
                    double pw = FPDF_GetPageWidthF(page), ph = FPDF_GetPageHeightF(page);
                    // Çok büyük sayfa: bellek taşmasın
                    s = Math.Min(s, maxSide / Math.Max(pw, ph));
                    int w = Math.Max(1, (int)Math.Round(pw * s)), h = Math.Max(1, (int)Math.Round(ph * s));
                    var bmp = FPDFBitmap_Create(w, h, 1);
                    if (bmp == IntPtr.Zero) throw new OutOfMemoryException();
                    try
                    {
                        FPDFBitmap_FillRect(bmp, 0, 0, w, h, 0xFFFFFFFF);
                        FPDF_RenderPageBitmap(bmp, page, 0, 0, w, h, 0, FPDF_ANNOT | FPDF_PRINTING);
                        int stride = FPDFBitmap_GetStride(bmp);
                        var buf = new byte[stride * h];
                        Marshal.Copy(FPDFBitmap_GetBuffer(bmp), buf, 0, buf.Length);
                        return (buf, w, h, stride);
                    }
                    finally { FPDFBitmap_Destroy(bmp); }
                }
                finally { FPDF_ClosePage(page); }
            }
        }

        public void Dispose()
        {
            lock (Lock) { if (H != IntPtr.Zero) { FPDF_CloseDocument(H); H = IntPtr.Zero; } }
            if (_pin.IsAllocated) _pin.Free();
        }
    }

    public static Doc Open(string path, string? password = null) => new(File.ReadAllBytes(path), password);
}
