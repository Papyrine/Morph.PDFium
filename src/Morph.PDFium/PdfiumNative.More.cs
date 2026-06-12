// Bindings from fpdf_save.h, fpdf_attachment.h, fpdf_signature.h and fpdf_annot.h.

static partial class PdfiumNative
{
    public const uint SaveIncremental = 1 << 0;
    public const uint SaveNoIncremental = 1 << 1;
    public const uint SaveRemoveSecurity = 1 << 2;

    // --- Save (fpdf_save.h) ---

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDF_SaveAsCopy(IntPtr document, in FileWrite fileWrite, uint flags);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDF_SaveWithVersion(IntPtr document, in FileWrite fileWrite, uint flags, int fileVersion);

    // --- Attachments (fpdf_attachment.h) ---

    [LibraryImport(library)]
    internal static partial int FPDFDoc_GetAttachmentCount(IntPtr document);

    [LibraryImport(library)]
    internal static partial IntPtr FPDFDoc_AddAttachment(IntPtr document, ReadOnlySpan<byte> name);

    [LibraryImport(library)]
    internal static partial IntPtr FPDFDoc_GetAttachment(IntPtr document, int index);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFDoc_DeleteAttachment(IntPtr document, int index);

    [LibraryImport(library)]
    internal static partial uint FPDFAttachment_GetName(IntPtr attachment, Span<byte> buffer, uint length);

    [LibraryImport(library)]
    internal static partial uint FPDFAttachment_GetSubtype(IntPtr attachment, Span<byte> buffer, uint length);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFAttachment_GetFile(IntPtr attachment, Span<byte> buffer, uint length, out uint outLength);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFAttachment_SetFile(IntPtr attachment, IntPtr document, ReadOnlySpan<byte> contents, uint length);

    // --- Signatures (fpdf_signature.h) ---

    [LibraryImport(library)]
    internal static partial int FPDF_GetSignatureCount(IntPtr document);

    [LibraryImport(library)]
    internal static partial IntPtr FPDF_GetSignatureObject(IntPtr document, int index);

    [LibraryImport(library)]
    internal static partial uint FPDFSignatureObj_GetContents(IntPtr signature, Span<byte> buffer, uint length);

    [LibraryImport(library)]
    internal static partial uint FPDFSignatureObj_GetByteRange(IntPtr signature, Span<int> buffer, uint length);

    [LibraryImport(library)]
    internal static partial uint FPDFSignatureObj_GetSubFilter(IntPtr signature, Span<byte> buffer, uint length);

    [LibraryImport(library)]
    internal static partial uint FPDFSignatureObj_GetReason(IntPtr signature, Span<byte> buffer, uint length);

    [LibraryImport(library)]
    internal static partial uint FPDFSignatureObj_GetTime(IntPtr signature, Span<byte> buffer, uint length);

    [LibraryImport(library)]
    internal static partial uint FPDFSignatureObj_GetDocMDPPermission(IntPtr signature);

    // --- Annotations (fpdf_annot.h) ---

    [LibraryImport(library)]
    internal static partial IntPtr FPDFPage_CreateAnnot(IntPtr page, int subtype);

    [LibraryImport(library)]
    internal static partial int FPDFPage_GetAnnotCount(IntPtr page);

    [LibraryImport(library)]
    internal static partial IntPtr FPDFPage_GetAnnot(IntPtr page, int index);

    [LibraryImport(library)]
    internal static partial void FPDFPage_CloseAnnot(IntPtr annot);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFPage_RemoveAnnot(IntPtr page, int index);

    [LibraryImport(library)]
    internal static partial int FPDFAnnot_GetSubtype(IntPtr annot);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFAnnot_GetRect(IntPtr annot, out FsRectF rect);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFAnnot_SetRect(IntPtr annot, in FsRectF rect);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFAnnot_GetColor(IntPtr annot, int colorType, out uint r, out uint g, out uint b, out uint a);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFAnnot_SetColor(IntPtr annot, int colorType, uint r, uint g, uint b, uint a);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFAnnot_HasKey(IntPtr annot, ReadOnlySpan<byte> key);

    [LibraryImport(library)]
    internal static partial uint FPDFAnnot_GetStringValue(IntPtr annot, ReadOnlySpan<byte> key, Span<byte> buffer, uint length);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFAnnot_SetStringValue(IntPtr annot, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value);

    /// <summary>
    /// Managed mirror of FPDF_FILEWRITE with an extra context slot. PDFium hands the callback
    /// a pointer to this struct (its <c>self</c> argument); we recover the destination stream
    /// through the trailing <see cref="Context"/> GCHandle.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct FileWrite
    {
        public int Version;
        public IntPtr WriteBlock;
        public IntPtr Context;
    }
}
