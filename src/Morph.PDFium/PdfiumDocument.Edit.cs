namespace Morph.PDFium;

public sealed partial class PdfiumDocument
{
    /// <summary>
    /// Creates an empty in-memory document with no pages. Use <see cref="NewPage"/> to add
    /// pages or <see cref="ImportPages(PdfiumDocument,string?,int)"/> to copy them in, then
    /// <see cref="Save(SaveFlags,int?)"/> to serialise the result.
    /// </summary>
    public static PdfiumDocument CreateNew()
    {
        lock (PdfiumNative.Sync)
        {
            var handle = PdfiumNative.FPDF_CreateNewDocument();
            if (handle == IntPtr.Zero)
            {
                throw new PdfiumException("Failed to create a new document");
            }

            return new(default, handle, 0);
        }
    }

    /// <summary>
    /// Inserts a new blank page of the given size (in points) at <paramref name="index"/> and
    /// returns it. The returned page must be disposed.
    /// </summary>
    public PdfPage NewPage(int index, double width, double height)
    {
        var doc = Handle;
        if (index < 0 || index > pageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, $"Page index must be in the range [0, {pageCount}]");
        }

        lock (PdfiumNative.Sync)
        {
            var page = PdfiumNative.FPDFPage_New(doc, index, width, height);
            if (page == IntPtr.Zero)
            {
                throw new PdfiumException($"Failed to create page at index {index}");
            }

            RefreshPageCount();
            return new(this, page, index);
        }
    }

    /// <summary>Deletes the page at <paramref name="index"/>.</summary>
    public void DeletePage(int index)
    {
        var doc = Handle;
        ValidateIndex(index);
        lock (PdfiumNative.Sync)
        {
            PdfiumNative.FPDFPage_Delete(doc, index);
            RefreshPageCount();
        }
    }

    /// <summary>
    /// Imports pages from <paramref name="source"/> into this document at <paramref name="index"/>.
    /// <paramref name="pageRange"/> is a 1-based range string such as "1,3,5-7"; pass null to
    /// import every page.
    /// </summary>
    public void ImportPages(PdfiumDocument source, string? pageRange = null, int index = 0)
    {
        ArgumentNullException.ThrowIfNull(source);
        var doc = Handle;
        var src = source.Handle;
        var range = pageRange is null ? default : Encoding.ASCII.GetBytes(pageRange + '\0');
        lock (PdfiumNative.Sync)
        {
            if (!PdfiumNative.FPDF_ImportPages(doc, src, range, index))
            {
                throw new PdfiumException("Failed to import pages (invalid page range?)");
            }

            RefreshPageCount();
        }
    }

    /// <summary>
    /// Imports the pages at the given zero-based <paramref name="pageIndices"/> from
    /// <paramref name="source"/> into this document at <paramref name="index"/>.
    /// </summary>
    public void ImportPages(PdfiumDocument source, ReadOnlySpan<int> pageIndices, int index = 0)
    {
        ArgumentNullException.ThrowIfNull(source);
        var doc = Handle;
        var src = source.Handle;
        lock (PdfiumNative.Sync)
        {
            if (!PdfiumNative.FPDF_ImportPagesByIndex(doc, src, pageIndices, (uint) pageIndices.Length, index))
            {
                throw new PdfiumException("Failed to import pages (invalid indices?)");
            }

            RefreshPageCount();
        }
    }

    /// <summary>Copies the viewer preferences (initial view settings) from <paramref name="source"/>.</summary>
    public void CopyViewerPreferences(PdfiumDocument source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var doc = Handle;
        var src = source.Handle;
        lock (PdfiumNative.Sync)
        {
            PdfiumNative.FPDF_CopyViewerPreferences(doc, src);
        }
    }
}
