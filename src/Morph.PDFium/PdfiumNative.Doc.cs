// Bindings from fpdf_doc.h: the document outline (bookmarks), destinations, actions
// and link annotations.

static partial class PdfiumNative
{
    [LibraryImport(library)]
    internal static partial IntPtr FPDFBookmark_GetFirstChild(IntPtr document, IntPtr bookmark);

    [LibraryImport(library)]
    internal static partial IntPtr FPDFBookmark_GetNextSibling(IntPtr document, IntPtr bookmark);

    [LibraryImport(library)]
    internal static partial uint FPDFBookmark_GetTitle(IntPtr bookmark, Span<byte> buffer, uint length);

    [LibraryImport(library)]
    internal static partial int FPDFBookmark_GetCount(IntPtr bookmark);

    [LibraryImport(library)]
    internal static partial IntPtr FPDFBookmark_Find(IntPtr document, ReadOnlySpan<byte> title);

    [LibraryImport(library)]
    internal static partial IntPtr FPDFBookmark_GetDest(IntPtr document, IntPtr bookmark);

    [LibraryImport(library)]
    internal static partial IntPtr FPDFBookmark_GetAction(IntPtr bookmark);

    [LibraryImport(library)]
    internal static partial uint FPDFAction_GetType(IntPtr action);

    [LibraryImport(library)]
    internal static partial IntPtr FPDFAction_GetDest(IntPtr document, IntPtr action);

    [LibraryImport(library)]
    internal static partial uint FPDFAction_GetFilePath(IntPtr action, Span<byte> buffer, uint length);

    [LibraryImport(library)]
    internal static partial uint FPDFAction_GetURIPath(IntPtr document, IntPtr action, Span<byte> buffer, uint length);

    [LibraryImport(library)]
    internal static partial int FPDFDest_GetDestPageIndex(IntPtr document, IntPtr dest);

    [LibraryImport(library)]
    internal static partial uint FPDFDest_GetView(IntPtr dest, out uint numParams, Span<float> parameters);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFDest_GetLocationInPage(IntPtr dest, out int hasX, out int hasY, out int hasZoom, out float x, out float y, out float zoom);

    [LibraryImport(library)]
    internal static partial IntPtr FPDFLink_GetLinkAtPoint(IntPtr page, double x, double y);

    [LibraryImport(library)]
    internal static partial int FPDFLink_GetLinkZOrderAtPoint(IntPtr page, double x, double y);

    [LibraryImport(library)]
    internal static partial IntPtr FPDFLink_GetDest(IntPtr document, IntPtr link);

    [LibraryImport(library)]
    internal static partial IntPtr FPDFLink_GetAction(IntPtr link);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFLink_Enumerate(IntPtr page, ref int startPos, out IntPtr linkAnnot);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFLink_GetAnnotRect(IntPtr linkAnnot, out FsRectF rect);

    [LibraryImport(library)]
    internal static partial int FPDFLink_CountQuadPoints(IntPtr linkAnnot);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFLink_GetQuadPoints(IntPtr linkAnnot, int quadIndex, out FsQuadPoints quadPoints);
}
