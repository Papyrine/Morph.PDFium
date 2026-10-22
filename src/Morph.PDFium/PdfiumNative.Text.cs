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
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFText_GetLooseCharBox(IntPtr textPage, int index, out FsRectF rect);

    // Returns 1 for a character PDFium inserted (the spaces and line breaks it infers from
    // the layout), 0 for one drawn by the page, -1 on error.
    [LibraryImport(library)]
    internal static partial int FPDFText_IsGenerated(IntPtr textPage, int index);

    [LibraryImport(library)]
    internal static partial uint FPDFText_GetFontInfo(IntPtr textPage, int index, Span<byte> buffer, uint length, out int flags);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFText_GetFillColor(IntPtr textPage, int index, out uint r, out uint g, out uint b, out uint a);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFText_GetStrokeColor(IntPtr textPage, int index, out uint r, out uint g, out uint b, out uint a);

    // The character's text matrix combined with the page's transformation, without the font
    // size: text set at size 1 and scaled by the matrix reports a font size of 1.
    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFText_GetMatrix(IntPtr textPage, int index, out FsMatrix matrix);

    [LibraryImport(library)]
    internal static partial IntPtr FPDFText_GetTextObject(IntPtr textPage, int index);

    // Text render modes (PDF 32000-1:2008, 9.3.6). Modes 4 to 7 add the text to the clipping
    // path but otherwise draw as modes 0 to 3, so 7, like 3, draws nothing.
    public const int TextRenderModeFill = 0;
    public const int TextRenderModeStroke = 1;
    public const int TextRenderModeFillStroke = 2;
    public const int TextRenderModeInvisible = 3;

    [LibraryImport(library)]
    internal static partial int FPDFTextObj_GetTextRenderMode(IntPtr textObject);

    [LibraryImport(library)]
    internal static partial IntPtr FPDFTextObj_GetFont(IntPtr textObject);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFTextObj_SetTextRenderMode(IntPtr textObject, int renderMode);

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
