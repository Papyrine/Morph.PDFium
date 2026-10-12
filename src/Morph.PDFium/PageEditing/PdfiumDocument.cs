namespace Morph.PDFium;

public sealed partial class PdfiumDocument
{
    /// <summary>
    /// Creates an empty in-memory document with no pages. Use <see cref="NewPage"/> to add
    /// pages or <see cref="ImportPages(PdfiumDocument,string?,int?)"/> to copy them in, then
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
        if (index < 0 || index > pageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, $"Page index must be in the range [0, {pageCount}]");
        }

        // PDFium accepts any numbers here and stores them as the page's media box, NaN included.
        if (!double.IsFinite(width) || width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "Page width must be positive and finite");
        }

        if (!double.IsFinite(height) || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "Page height must be positive and finite");
        }

        lock (PdfiumNative.Sync)
        {
            var doc = Handle;
            var page = PdfiumNative.FPDFPage_New(doc, index, width, height);
            if (page == IntPtr.Zero)
            {
                throw new PdfiumException($"Failed to create page at index {index}");
            }

            ShiftOpenPages(index, 1);
            RefreshPageCount();
            return new(this, page, index);
        }
    }

    /// <summary>Deletes the page at <paramref name="index"/>.</summary>
    public void DeletePage(int index)
    {
        lock (PdfiumNative.Sync)
        {
            var doc = Handle;
            ValidateIndex(index);
            PdfiumNative.FPDFPage_Delete(doc, index);
            foreach (var child in children)
            {
                if (child is PdfPage page && page.Index == index)
                {
                    page.Index = -1;
                }
            }

            ShiftOpenPages(index + 1, -1);
            RefreshPageCount();
        }
    }

    /// <summary>
    /// Imports pages from <paramref name="source"/> into this document, inserting them before
    /// the page at <paramref name="index"/>, or appending them when it is null.
    /// <paramref name="pageRange"/> is a 1-based range string such as "1,3,5-7"; pass null to
    /// import every page.
    /// </summary>
    public void ImportPages(PdfiumDocument source, string? pageRange = null, int? index = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        var range = pageRange is null ? default : Encoding.ASCII.GetBytes(pageRange + '\0');
        lock (PdfiumNative.Sync)
        {
            var doc = Handle;
            var src = source.Handle;
            source.GeneratePendingContent();
            if (!PdfiumNative.FPDF_ImportPages(doc, src, range, index ?? pageCount))
            {
                throw new PdfiumException("Failed to import pages (invalid page range?)");
            }

            var before = pageCount;
            RefreshPageCount();
            ShiftOpenPages(index ?? before, pageCount - before);
        }
    }

    /// <summary>
    /// Imports the pages at the given zero-based <paramref name="pageIndices"/> from
    /// <paramref name="source"/> into this document, inserting them before the page at
    /// <paramref name="index"/>, or appending them when it is null.
    /// </summary>
    public void ImportPages(PdfiumDocument source, ReadOnlySpan<int> pageIndices, int? index = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        lock (PdfiumNative.Sync)
        {
            var doc = Handle;
            var src = source.Handle;
            source.GeneratePendingContent();
            if (!PdfiumNative.FPDF_ImportPagesByIndex(doc, src, pageIndices, (uint) pageIndices.Length, index ?? pageCount))
            {
                throw new PdfiumException("Failed to import pages (invalid indices?)");
            }

            var before = pageCount;
            RefreshPageCount();
            ShiftOpenPages(index ?? before, pageCount - before);
        }
    }

    // Caller must hold PdfiumNative.Sync. Keeps PdfPage.Index right for pages that are open
    // when the page list changes: every open page at or after <paramref name="index"/> moves
    // by <paramref name="count"/>.
    void ShiftOpenPages(int index, int count)
    {
        foreach (var child in children)
        {
            if (child is PdfPage page && page.Index >= index)
            {
                page.Index += count;
            }
        }
    }

    /// <summary>Copies the viewer preferences (initial view settings) from <paramref name="source"/>.</summary>
    public void CopyViewerPreferences(PdfiumDocument source)
    {
        ArgumentNullException.ThrowIfNull(source);
        lock (PdfiumNative.Sync)
        {
            var doc = Handle;
            var src = source.Handle;
            PdfiumNative.FPDF_CopyViewerPreferences(doc, src);
        }
    }
}
