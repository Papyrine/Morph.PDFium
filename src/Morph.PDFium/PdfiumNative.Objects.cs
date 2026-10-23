// Bindings from fpdf_edit.h (page objects), fpdf_structtree.h (tagged-PDF tree) and
// fpdf_thumbnail.h (embedded page thumbnails).

static partial class PdfiumNative
{
    // --- Page objects (fpdf_edit.h) ---

    [LibraryImport(library)]
    internal static partial IntPtr FPDFPage_GetObject(IntPtr page, int index);

    [LibraryImport(library)]
    internal static partial int FPDFPageObj_GetType(IntPtr pageObject);

    // The objects inside a form XObject (page object type 5), which can themselves be forms.
    [LibraryImport(library)]
    internal static partial int FPDFFormObj_CountObjects(IntPtr formObject);

    [LibraryImport(library)]
    internal static partial IntPtr FPDFFormObj_GetObject(IntPtr formObject, uint index);

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
    internal static partial bool FPDFPageObj_GetStrokeWidth(IntPtr pageObject, out float width);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFPageObj_SetStrokeColor(IntPtr pageObject, uint r, uint g, uint b, uint a);

    [LibraryImport(library)]
    internal static partial IntPtr FPDFPageObj_NewTextObj(IntPtr document, ReadOnlySpan<byte> font, float fontSize);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFText_SetText(IntPtr textObject, ReadOnlySpan<byte> text);

    public const int FillModeWinding = 2;

    [LibraryImport(library)]
    internal static partial IntPtr FPDFPageObj_CreateNewRect(float x, float y, float w, float h);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFPath_SetDrawMode(IntPtr path, int fillMode, [MarshalAs(UnmanagedType.Bool)] bool stroke);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFPage_InsertObject(IntPtr page, IntPtr pageObject);

    [LibraryImport(library)]
    internal static partial void FPDFPageObj_Destroy(IntPtr pageObject);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFPageObj_GetMatrix(IntPtr pageObject, out FsMatrix matrix);

    // An inactive object stays on its page but is drawn, and written, as if it were not there.
    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFPageObj_SetIsActive(IntPtr pageObject, [MarshalAs(UnmanagedType.Bool)] bool active);

    // --- Marked content (fpdf_edit.h) ---

    // The marks of every marked-content sequence an object sits in, innermost last. Those of
    // the content stream that draws a form XObject are not repeated on the form's own objects.
    [LibraryImport(library)]
    internal static partial int FPDFPageObj_CountMarks(IntPtr pageObject);

    [LibraryImport(library)]
    internal static partial IntPtr FPDFPageObj_GetMark(IntPtr pageObject, uint index);

    // The mark's tag, in UTF-16LE; outLength is the bytes it needs, terminator included.
    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFPageObjMark_GetName(IntPtr mark, Span<byte> buffer, uint length, out uint outLength);

    public const int ObjectString = 3;
    public const int ObjectName = 4;

    [LibraryImport(library)]
    internal static partial int FPDFPageObjMark_GetParamValueType(IntPtr mark, ReadOnlySpan<byte> key);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFPageObjMark_GetParamStringValue(IntPtr mark, ReadOnlySpan<byte> key, Span<byte> buffer, uint length, out uint outLength);

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
