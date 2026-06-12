// Bindings from fpdfview.h, fpdf_doc.h and fpdf_ext.h for document-level metadata
// beyond the information dictionary: file version, permissions, page mode, labels
// and the trailer file identifier.

static partial class PdfiumNative
{
    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDF_GetFileVersion(IntPtr document, out int fileVersion);

    [LibraryImport(library)]
    internal static partial uint FPDF_GetDocPermissions(IntPtr document);

    [LibraryImport(library)]
    internal static partial uint FPDF_GetDocUserPermissions(IntPtr document);

    [LibraryImport(library)]
    internal static partial int FPDF_GetSecurityHandlerRevision(IntPtr document);

    [LibraryImport(library)]
    internal static partial int FPDFDoc_GetPageMode(IntPtr document);

    [LibraryImport(library)]
    internal static partial uint FPDF_GetPageLabel(IntPtr document, int pageIndex, Span<byte> buffer, uint length);

    [LibraryImport(library)]
    internal static partial uint FPDF_GetFileIdentifier(IntPtr document, int idType, Span<byte> buffer, uint length);
}
