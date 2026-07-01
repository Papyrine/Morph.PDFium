// Bindings from fpdf_edit.h (page objects), fpdf_structtree.h (tagged-PDF tree) and
// fpdf_thumbnail.h (embedded page thumbnails).

static partial class PdfiumNative
{
    // --- Page objects (fpdf_edit.h) ---

    [LibraryImport(library)]
    internal static partial IntPtr FPDFPage_GetObject(IntPtr page, int index);

    [LibraryImport(library)]
    internal static partial int FPDFPageObj_GetType(IntPtr pageObject);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFPageObj_GetBounds(IntPtr pageObject, out float left, out float bottom, out float right, out float top);

    [LibraryImport(library)]
    internal static partial void FPDFPageObj_Transform(IntPtr pageObject, double a, double b, double c, double d, double e, double f);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFPageObj_SetFillColor(IntPtr pageObject, uint r, uint g, uint b, uint a);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFPageObj_SetStrokeColor(IntPtr pageObject, uint r, uint g, uint b, uint a);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFPageObj_SetStrokeWidth(IntPtr pageObject, float width);

    [LibraryImport(library)]
    internal static partial IntPtr FPDFPageObj_NewTextObj(IntPtr document, ReadOnlySpan<byte> font, float fontSize);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFText_SetText(IntPtr textObject, ReadOnlySpan<byte> text);

    public const int FillModeNone = 0;
    public const int FillModeWinding = 2;

    [LibraryImport(library)]
    internal static partial IntPtr FPDFPageObj_CreateNewRect(float x, float y, float w, float h);

    [LibraryImport(library)]
    internal static partial IntPtr FPDFPageObj_CreateNewPath(float x, float y);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFPath_LineTo(IntPtr path, float x, float y);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFPath_Close(IntPtr path);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFPath_SetDrawMode(IntPtr path, int fillMode, [MarshalAs(UnmanagedType.Bool)] bool stroke);

    [LibraryImport(library)]
    internal static partial IntPtr FPDFPageObj_NewImageObj(IntPtr document);

    // pages/count let PDFium invalidate cached renderings of the affected pages; the bitmap is
    // copied into the image object, so the caller may destroy it once this returns.
    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFImageObj_SetBitmap(ReadOnlySpan<IntPtr> pages, int count, IntPtr imageObject, IntPtr bitmap);

    [LibraryImport(library)]
    internal static partial void FPDFPage_InsertObject(IntPtr page, IntPtr pageObject);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFPage_RemoveObject(IntPtr page, IntPtr pageObject);

    [LibraryImport(library)]
    internal static partial void FPDFPageObj_Destroy(IntPtr pageObject);

    // --- Structure tree (fpdf_structtree.h) ---

    [LibraryImport(library)]
    internal static partial IntPtr FPDF_StructTree_GetForPage(IntPtr page);

    [LibraryImport(library)]
    internal static partial void FPDF_StructTree_Close(IntPtr structTree);

    [LibraryImport(library)]
    internal static partial int FPDF_StructTree_CountChildren(IntPtr structTree);

    [LibraryImport(library)]
    internal static partial IntPtr FPDF_StructTree_GetChildAtIndex(IntPtr structTree, int index);

    [LibraryImport(library)]
    internal static partial uint FPDF_StructElement_GetType(IntPtr element, Span<byte> buffer, uint length);

    [LibraryImport(library)]
    internal static partial uint FPDF_StructElement_GetTitle(IntPtr element, Span<byte> buffer, uint length);

    [LibraryImport(library)]
    internal static partial uint FPDF_StructElement_GetAltText(IntPtr element, Span<byte> buffer, uint length);

    [LibraryImport(library)]
    internal static partial int FPDF_StructElement_CountChildren(IntPtr element);

    [LibraryImport(library)]
    internal static partial IntPtr FPDF_StructElement_GetChildAtIndex(IntPtr element, int index);

    // --- Thumbnails (fpdf_thumbnail.h) ---

    [LibraryImport(library)]
    internal static partial uint FPDFPage_GetDecodedThumbnailData(IntPtr page, Span<byte> buffer, uint length);
}
