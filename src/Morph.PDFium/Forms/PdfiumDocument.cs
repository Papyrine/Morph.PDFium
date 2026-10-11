namespace Morph.PDFium;

public sealed partial class PdfiumDocument
{
    /// <summary>The kind of interactive form the document carries (or <see cref="FormType.None"/>).</summary>
    public FormType GetFormType()
    {
        lock (PdfiumNative.Sync)
        {
            var doc = Handle;
            return (FormType) PdfiumNative.FPDF_GetFormType(doc);
        }
    }

    /// <summary>
    /// Starts an interactive form-fill session, required to read AcroForm field values and to
    /// render filled-in widgets. Dispose the returned <see cref="PdfForm"/> before this document.
    /// </summary>
    public PdfForm LoadForm() =>
        PdfForm.Create(this);
}
