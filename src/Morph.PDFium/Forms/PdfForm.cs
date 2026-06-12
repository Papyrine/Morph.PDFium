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

/// <summary>A single interactive form field (widget) on a page.</summary>
/// <param name="Name">The fully qualified field name.</param>
/// <param name="Type">The field type.</param>
/// <param name="Value">The field's current value, when it has one.</param>
/// <param name="Rectangle">The widget's bounding rectangle in page points.</param>
public readonly record struct PdfFormField(string Name, FormFieldType Type, string? Value, PdfRectangle Rectangle);

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
    }

    internal static PdfForm Create(PdfiumDocument document)
    {
        var doc = document.Handle;
        var info = new PdfiumNative.FormFillInfo[] { new() { Version = 2 } };
        var pinned = GCHandle.Alloc(info, GCHandleType.Pinned);
        lock (PdfiumNative.Sync)
        {
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
        var form = ValidHandle();
        lock (PdfiumNative.Sync)
        {
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
        var form = ValidHandle();
        var pageHandle = page.ValidHandle();
        lock (PdfiumNative.Sync)
        {
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
                    var name = Interop.Utf16ByLength((buffer, length) => PdfiumNative.FPDFAnnot_GetFormFieldName(form, annot, buffer, length)) ?? string.Empty;
                    var value = Interop.Utf16ByLength((buffer, length) => PdfiumNative.FPDFAnnot_GetFormFieldValue(form, annot, buffer, length));
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
        var form = ValidHandle();
        var (pixels, width, height) = document.RenderPixels(index, options.Dpi, options.ToFlags(), null, form);
        return PngEncoder.Encode(pixels, width, height, options.Dpi);
    }

    public void Dispose()
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        lock (PdfiumNative.Sync)
        {
            PdfiumNative.FPDFDOC_ExitFormFillEnvironment(handle);
        }

        handle = IntPtr.Zero;
        if (pinnedInfo.IsAllocated)
        {
            pinnedInfo.Free();
        }
    }
}
