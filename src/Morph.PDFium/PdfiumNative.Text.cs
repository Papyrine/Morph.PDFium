// Bindings from fpdf_text.h: per-page text extraction, character geometry, search and
// the implicit "web link" detector that runs over extracted text.

static partial class PdfiumNative
{
    public const uint MatchCase = 0x01;
    public const uint MatchWholeWord = 0x02;
    public const uint MatchConsecutive = 0x04;

    [LibraryImport(library)]
    internal static partial IntPtr FPDFText_LoadPage(IntPtr page);

    [LibraryImport(library)]
    internal static partial void FPDFText_ClosePage(IntPtr textPage);

    [LibraryImport(library)]
    internal static partial int FPDFText_CountChars(IntPtr textPage);

    [LibraryImport(library)]
    internal static partial uint FPDFText_GetUnicode(IntPtr textPage, int index);

    [LibraryImport(library)]
    internal static partial double FPDFText_GetFontSize(IntPtr textPage, int index);

    [LibraryImport(library)]
    internal static partial int FPDFText_GetFontWeight(IntPtr textPage, int index);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFText_GetCharBox(IntPtr textPage, int index, out double left, out double right, out double bottom, out double top);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFText_GetCharOrigin(IntPtr textPage, int index, out double x, out double y);

    [LibraryImport(library)]
    internal static partial float FPDFText_GetCharAngle(IntPtr textPage, int index);

    [LibraryImport(library)]
    internal static partial int FPDFText_GetCharIndexAtPos(IntPtr textPage, double x, double y, double xTolerance, double yTolerance);

    [LibraryImport(library)]
    internal static partial int FPDFText_GetText(IntPtr textPage, int startIndex, int count, Span<ushort> result);

    [LibraryImport(library)]
    internal static partial int FPDFText_CountRects(IntPtr textPage, int startIndex, int count);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFText_GetRect(IntPtr textPage, int rectIndex, out double left, out double top, out double right, out double bottom);

    [LibraryImport(library)]
    internal static partial int FPDFText_GetBoundedText(IntPtr textPage, double left, double top, double right, double bottom, Span<ushort> buffer, int bufferLength);

    [LibraryImport(library)]
    internal static partial IntPtr FPDFText_FindStart(IntPtr textPage, ReadOnlySpan<byte> findWhat, uint flags, int startIndex);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFText_FindNext(IntPtr handle);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFText_FindPrev(IntPtr handle);

    [LibraryImport(library)]
    internal static partial int FPDFText_GetSchResultIndex(IntPtr handle);

    [LibraryImport(library)]
    internal static partial int FPDFText_GetSchCount(IntPtr handle);

    [LibraryImport(library)]
    internal static partial void FPDFText_FindClose(IntPtr handle);

    [LibraryImport(library)]
    internal static partial IntPtr FPDFLink_LoadWebLinks(IntPtr textPage);

    [LibraryImport(library)]
    internal static partial int FPDFLink_CountWebLinks(IntPtr linkPage);

    [LibraryImport(library)]
    internal static partial int FPDFLink_GetURL(IntPtr linkPage, int linkIndex, Span<ushort> buffer, int bufferLength);

    [LibraryImport(library)]
    internal static partial int FPDFLink_CountRects(IntPtr linkPage, int linkIndex);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFLink_GetRect(IntPtr linkPage, int linkIndex, int rectIndex, out double left, out double top, out double right, out double bottom);

    [LibraryImport(library)]
    internal static partial void FPDFLink_CloseWebLinks(IntPtr linkPage);

    // Page geometry from fpdfview.h, used by PdfPage.
    [LibraryImport(library)]
    internal static partial float FPDF_GetPageWidthF(IntPtr page);

    [LibraryImport(library)]
    internal static partial float FPDF_GetPageHeightF(IntPtr page);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDF_GetPageBoundingBox(IntPtr page, out FsRectF rect);
}
