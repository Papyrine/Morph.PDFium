namespace Morph.PDFium;

public sealed partial class PdfiumDocument
{
    /// <summary>
    /// Loads the page at <paramref name="index"/> for text, link and annotation access.
    /// The returned <see cref="PdfPage"/> owns native resources and must be disposed.
    /// </summary>
    public PdfPage LoadPage(int index)
    {
        lock (PdfiumNative.Sync)
        {
            var doc = Handle;
            ValidateIndex(index);
            var page = PdfiumNative.FPDF_LoadPage(doc, index);
            if (page == IntPtr.Zero)
            {
                throw new PdfiumException($"Failed to load page {index}");
            }

            return new(this, page, index);
        }
    }
}
