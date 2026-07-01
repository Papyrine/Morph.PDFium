/// <summary>
/// P/Invoke surface over the PDFium C API. The native binaries ship via the
/// bblanchon.PDFium.* packages (https://github.com/bblanchon/pdfium-binaries).
/// PDFium is not thread safe, so every call must be made while holding <see cref="Sync"/>.
/// The library is initialized once by the static constructor and never torn down:
/// FPDF_DestroyLibrary is process-wide and would invalidate documents still in flight.
/// </summary>
static partial class PdfiumNative
{
    public const int FormatBgra = 4;
    public const int RenderAnnotations = 0x01;
    public const int ReverseByteOrder = 0x10;

    public static readonly Lock Sync = new();

    static PdfiumNative()
    {
        lock (Sync)
        {
            FPDF_InitLibrary();
        }
    }

    const string library = "pdfium";

    [LibraryImport(library)]
    internal static partial void FPDF_InitLibrary();

    [LibraryImport(library)]
    internal static partial IntPtr FPDF_LoadMemDocument(IntPtr data, int size, IntPtr password);

    [LibraryImport(library)]
    internal static partial uint FPDF_GetLastError();

    [LibraryImport(library)]
    internal static partial int FPDF_GetPageCount(IntPtr document);

    [LibraryImport(library)]
    internal static partial int FPDF_GetPageSizeByIndexF(IntPtr document, int index, out PageSizeF size);

    [LibraryImport(library)]
    internal static partial IntPtr FPDF_LoadPage(IntPtr document, int index);

    [LibraryImport(library)]
    internal static partial void FPDF_ClosePage(IntPtr page);

    [LibraryImport(library)]
    internal static partial void FPDF_CloseDocument(IntPtr document);

    // The native buflen parameter is declared `unsigned long`, which is 32 bit on
    // Windows and 64 bit elsewhere; uint is safe for both since lengths are small
    // and x64/arm64 calling conventions zero-extend 32 bit arguments.
    [LibraryImport(library)]
    internal static partial uint FPDF_GetMetaText(IntPtr document, ReadOnlySpan<byte> tag, Span<byte> buffer, uint length);

    [LibraryImport(library)]
    internal static partial IntPtr FPDFBitmap_CreateEx(int width, int height, int format, IntPtr firstScan, int stride);

    // Allocates a PDFium-owned bitmap (alpha != 0 => BGRA, else BGRx; always 4 bytes/pixel).
    [LibraryImport(library)]
    internal static partial IntPtr FPDFBitmap_Create(int width, int height, int alpha);

    [LibraryImport(library)]
    internal static partial IntPtr FPDFBitmap_GetBuffer(IntPtr bitmap);

    [LibraryImport(library)]
    internal static partial int FPDFBitmap_FillRect(IntPtr bitmap, int left, int top, int width, int height, uint color);

    [LibraryImport(library)]
    internal static partial void FPDF_RenderPageBitmap(IntPtr bitmap, IntPtr page, int startX, int startY, int sizeX, int sizeY, int rotate, int flags);

    [LibraryImport(library)]
    internal static partial void FPDFBitmap_Destroy(IntPtr bitmap);

    /// <summary>Mirrors the native FS_SIZEF struct.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct PageSizeF
    {
        public float Width;
        public float Height;
    }
}
