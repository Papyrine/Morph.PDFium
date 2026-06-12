// Bindings from fpdf_edit.h, fpdf_ppo.h and fpdf_flatten.h: document/page creation and
// mutation, page rotation, content generation, page import/merge and flattening.

static partial class PdfiumNative
{
    public const int FlatNormalDisplay = 0;
    public const int FlatPrint = 1;

    [LibraryImport(library)]
    internal static partial IntPtr FPDF_CreateNewDocument();

    [LibraryImport(library)]
    internal static partial IntPtr FPDFPage_New(IntPtr document, int pageIndex, double width, double height);

    [LibraryImport(library)]
    internal static partial void FPDFPage_Delete(IntPtr document, int pageIndex);

    [LibraryImport(library)]
    internal static partial int FPDFPage_GetRotation(IntPtr page);

    [LibraryImport(library)]
    internal static partial void FPDFPage_SetRotation(IntPtr page, int rotate);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFPage_GenerateContent(IntPtr page);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFPage_HasTransparency(IntPtr page);

    [LibraryImport(library)]
    internal static partial int FPDFPage_CountObjects(IntPtr page);

    [LibraryImport(library)]
    internal static partial int FPDFPage_Flatten(IntPtr page, int flag);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDF_ImportPages(IntPtr destDoc, IntPtr srcDoc, ReadOnlySpan<byte> pageRange, int index);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDF_ImportPagesByIndex(IntPtr destDoc, IntPtr srcDoc, ReadOnlySpan<int> pageIndices, uint length, int index);

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDF_CopyViewerPreferences(IntPtr destDoc, IntPtr srcDoc);
}
