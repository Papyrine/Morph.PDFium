namespace Morph.PDFium;

/// <summary>The kind of interactive form a document carries.</summary>
public enum FormType
{
    /// <summary>The document has no interactive form.</summary>
    None = 0,

    /// <summary>An AcroForm (the standard PDF interactive form).</summary>
    AcroForm = 1,

    /// <summary>A full XFA form (not supported by the default PDFium build).</summary>
    XfaFull = 2,

    /// <summary>An XFA foreground form (not supported by the default PDFium build).</summary>
    XfaForeground = 3
}

/// <summary>The type of an interactive form field.</summary>
public enum FormFieldType
{
    Unknown = 0,
    PushButton = 1,
    CheckBox = 2,
    RadioButton = 3,
    ComboBox = 4,
    ListBox = 5,
    TextField = 6,
    Signature = 7
}

/// <summary>
/// An interactive form-fill session over a document. Required for reading AcroForm field
/// values and for rendering filled-in widgets. Obtain one with
/// <see cref="PdfiumDocument.LoadForm"/> and dispose it before the document.
/// </summary>
public sealed class PdfForm :
    IDisposable
{
    readonly PdfiumDocument document;
    IntPtr handle;
    // The FORMFILLINFO must outlive the form handle (PDFium retains the pointer), so it is
    // pinned for the session and freed alongside the handle.
    GCHandle pinnedInfo;

    PdfForm(PdfiumDocument document, IntPtr handle, GCHandle pinnedInfo)
    {
        this.document = document;
        this.handle = handle;
        this.pinnedInfo = pinnedInfo;
        document.Track(this);
    }

    internal static PdfForm Create(PdfiumDocument document)
    {
        lock (PdfiumNative.Sync)
        {
            var doc = document.Handle;
            var info = new PdfiumNative.FormFillInfo[] { new() { Version = 2 } };
            var pinned = GCHandle.Alloc(info, GCHandleType.Pinned);
            var handle = PdfiumNative.FPDFDOC_InitFormFillEnvironment(doc, pinned.AddrOfPinnedObject());
            if (handle == IntPtr.Zero)
            {
                pinned.Free();
                throw new PdfiumException("Failed to initialize the form environment (document has no form?)");
            }

            return new(document, handle, pinned);
        }
    }

    IntPtr ValidHandle()
    {
        ObjectDisposedException.ThrowIf(handle == IntPtr.Zero, this);
        return handle;
    }

    /// <summary>Sets the highlight color (RGB) and opacity drawn behind form fields when rendering.</summary>
    public void SetHighlight(PdfColor color)
    {
        lock (PdfiumNative.Sync)
        {
            var form = ValidHandle();
            // FPDF_FORMFIELD_UNKNOWN (0) applies the color to every field type.
            var rgb = ((uint) color.R << 16) | ((uint) color.G << 8) | color.B;
            PdfiumNative.FPDF_SetFormFieldHighlightColor(form, 0, rgb);
            PdfiumNative.FPDF_SetFormFieldHighlightAlpha(form, color.A);
        }
    }

    /// <summary>Reads the interactive form fields (widgets) present on the given page.</summary>
    public IReadOnlyList<PdfFormField> GetFields(PdfPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        lock (PdfiumNative.Sync)
        {
            var form = ValidHandle();
            var pageHandle = page.ValidHandle();
            var count = PdfiumNative.FPDFPage_GetAnnotCount(pageHandle);
            if (count <= 0)
            {
                return [];
            }

            var fields = new List<PdfFormField>();
            for (var index = 0; index < count; index++)
            {
                var annot = PdfiumNative.FPDFPage_GetAnnot(pageHandle, index);
                if (annot == IntPtr.Zero)
                {
                    continue;
                }

                try
                {
                    // Only widget annotations (subtype 20) are form fields.
                    if (PdfiumNative.FPDFAnnot_GetSubtype(annot) != 20)
                    {
                        continue;
                    }

                    var type = (FormFieldType) PdfiumNative.FPDFAnnot_GetFormFieldType(form, annot);
                    var name = Interop.Utf16ByLength((form, annot), static (state, buffer, length) => PdfiumNative.FPDFAnnot_GetFormFieldName(state.form, state.annot, buffer, length)) ?? string.Empty;
                    var value = Interop.Utf16ByLength((form, annot), static (state, buffer, length) => PdfiumNative.FPDFAnnot_GetFormFieldValue(state.form, state.annot, buffer, length));
                    var rectangle = PdfiumNative.FPDFAnnot_GetRect(annot, out var rect)
                        ? new PdfRectangle(rect.Left, rect.Bottom, rect.Right, rect.Top)
                        : default;
                    fields.Add(new(name, type, value, rectangle));
                }
                finally
                {
                    PdfiumNative.FPDFPage_CloseAnnot(annot);
                }
            }

            return fields;
        }
    }

    /// <summary>
    /// Renders the page at <paramref name="index"/> to a PNG, including interactive form
    /// fields drawn over the page content (via FPDF_FFLDraw).
    /// </summary>
    public byte[] RenderPage(int index, RenderOptions? options = null)
    {
        options ??= new();
        if (!double.IsFinite(options.Dpi) || options.Dpi <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.Dpi, "dpi must be positive and finite");
        }

        byte[] pixels;
        int width;
        int height;
        // Held across the render so the form cannot be disposed between the check and its use.
        lock (PdfiumNative.Sync)
        {
            var form = ValidHandle();
            // Touch the document so a disposed document surfaces as ObjectDisposedException.
            _ = document.Handle;
            document.ValidateIndex(index);
            (pixels, width, height) = document.RenderPixels(index, options.Dpi, options.ToFlags(), options.ToFillColor(), null, form);
        }

        return PngEncoder.Encode(pixels, width, height, options.Dpi);
    }

    public void Dispose()
    {
        // Checked and cleared under the lock so a call on another thread either completes
        // first or sees the form as disposed.
        lock (PdfiumNative.Sync)
        {
            if (handle == IntPtr.Zero)
            {
                return;
            }

            PdfiumNative.FPDFDOC_ExitFormFillEnvironment(handle);
            document.Untrack(this);
            handle = IntPtr.Zero;
            if (pinnedInfo.IsAllocated)
            {
                pinnedInfo.Free();
            }
        }
    }
}
