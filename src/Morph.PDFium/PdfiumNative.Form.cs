// Bindings from fpdf_formfill.h: the form-fill environment, widget rendering and the
// per-widget form-field accessors from fpdf_annot.h that need a form handle.

static partial class PdfiumNative
{
    [LibraryImport(library)]
    internal static partial int FPDF_GetFormType(IntPtr document);

    // formInfo must remain alive for the lifetime of the returned handle: PDFium keeps the
    // pointer and may invoke its callbacks later. Callers pass a pinned/native address.
    [LibraryImport(library)]
    internal static partial IntPtr FPDFDOC_InitFormFillEnvironment(IntPtr document, IntPtr formInfo);

    [LibraryImport(library)]
    internal static partial void FPDFDOC_ExitFormFillEnvironment(IntPtr formHandle);

    [LibraryImport(library)]
    internal static partial void FORM_OnAfterLoadPage(IntPtr page, IntPtr formHandle);

    [LibraryImport(library)]
    internal static partial void FORM_OnBeforeClosePage(IntPtr page, IntPtr formHandle);

    [LibraryImport(library)]
    internal static partial void FPDF_FFLDraw(IntPtr formHandle, IntPtr bitmap, IntPtr page, int startX, int startY, int sizeX, int sizeY, int rotate, int flags);

    [LibraryImport(library)]
    internal static partial void FPDF_SetFormFieldHighlightColor(IntPtr formHandle, int fieldType, uint color);

    [LibraryImport(library)]
    internal static partial void FPDF_SetFormFieldHighlightAlpha(IntPtr formHandle, byte alpha);

    [LibraryImport(library)]
    internal static partial uint FPDFAnnot_GetFormFieldName(IntPtr formHandle, IntPtr annot, Span<byte> buffer, uint length);

    [LibraryImport(library)]
    internal static partial uint FPDFAnnot_GetFormFieldValue(IntPtr formHandle, IntPtr annot, Span<byte> buffer, uint length);

    [LibraryImport(library)]
    internal static partial int FPDFAnnot_GetFormFieldType(IntPtr formHandle, IntPtr annot);

    /// <summary>
    /// Managed mirror of FPDF_FORMFILLINFO. The struct is one <c>version</c> int followed by 32
    /// callback function pointers. We leave every callback null: this is sufficient for
    /// headless reading of field values and for rendering existing widget appearances with
    /// <see cref="FPDF_FFLDraw"/>. The full slot count must be present so PDFium does not read
    /// past the struct when <c>Version</c> is 2.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct FormFillInfo
    {
        public int Version;
        public IntPtr Release;
        public IntPtr FFI_Invalidate;
        public IntPtr FFI_OutputSelectedRect;
        public IntPtr FFI_SetCursor;
        public IntPtr FFI_SetTimer;
        public IntPtr FFI_KillTimer;
        public IntPtr FFI_GetLocalTime;
        public IntPtr FFI_OnChange;
        public IntPtr FFI_GetPage;
        public IntPtr FFI_GetCurrentPage;
        public IntPtr FFI_GetRotation;
        public IntPtr FFI_ExecuteNamedAction;
        public IntPtr FFI_SetTextFieldFocus;
        public IntPtr FFI_DoURIAction;
        public IntPtr FFI_DoGoToAction;
        // Version 2.
        public IntPtr FFI_DisplayCaret;
        public IntPtr FFI_GetCurrentPageIndex;
        public IntPtr FFI_SetCurrentPage;
        public IntPtr FFI_GotoURL;
        public IntPtr FFI_GetPageViewRect;
        public IntPtr FFI_PageEvent;
        public IntPtr FFI_PopupMenu;
        public IntPtr FFI_OpenFile;
        public IntPtr FFI_EmailTo;
        public IntPtr FFI_UploadTo;
        public IntPtr FFI_GetPlatform;
        public IntPtr FFI_GetLanguage;
        public IntPtr FFI_DownloadFromURL;
        public IntPtr FFI_PostRequestURL;
        public IntPtr FFI_PutRequestURL;
        public IntPtr FFI_OnFocusChange;
        public IntPtr FFI_DoURIActionWithKeyboardModifier;
    }
}
