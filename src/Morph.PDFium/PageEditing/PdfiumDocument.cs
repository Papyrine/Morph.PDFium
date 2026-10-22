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
            // Under the lock, so a page deleted on another thread cannot leave the new page
            // reporting an index past the end. PDFium clamps rather than failing.
            if (index < 0 || index > pageCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, $"Page index must be in the range [0, {pageCount}]");
            }

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
    /// import every page. <paramref name="source"/> may be this document itself.
    /// </summary>
    // Preferred over the span overload, so that a null range picks this one rather than being
    // ambiguous between the two.
    [OverloadResolutionPriority(1)]
    public void ImportPages(PdfiumDocument source, string? pageRange = null, int? index = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        // PDFium reads an empty range the same as no range at all, and would import everything.
        if (pageRange is not null && string.IsNullOrWhiteSpace(pageRange))
        {
            throw new ArgumentException("Page range must name at least one page; pass null to import every page.", nameof(pageRange));
        }

        var range = pageRange is null ? default : Encoding.ASCII.GetBytes(pageRange + '\0');
        lock (PdfiumNative.Sync)
        {
            if (!Import(source, index, (destination, from, at) => PdfiumNative.FPDF_ImportPages(destination, from, range, at)))
            {
                throw new PdfiumException("Failed to import pages (invalid page range?)");
            }
        }
    }

    /// <summary>
    /// Imports the pages at the given zero-based <paramref name="pageIndices"/> from
    /// <paramref name="source"/> into this document, inserting them before the page at
    /// <paramref name="index"/>, or appending them when it is null. An empty list imports
    /// nothing. <paramref name="source"/> may be this document itself.
    /// </summary>
    public void ImportPages(PdfiumDocument source, ReadOnlySpan<int> pageIndices, int? index = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        lock (PdfiumNative.Sync)
        {
            _ = Handle;
            var count = source.PageCount;
            // PDFium does not check these first: it inserts a blank page for an index past the
            // source's end, and only then fails, leaving the blank page behind.
            foreach (var pageIndex in pageIndices)
            {
                if (pageIndex < 0 || pageIndex >= count)
                {
                    throw new ArgumentOutOfRangeException(nameof(pageIndices), pageIndex, $"Page indices must be in the range [0, {count - 1}] of the source document");
                }
            }

            // Nothing to import. This cannot be left to PDFium: an empty span may reach it as
            // a null pointer, which it takes to mean every page.
            if (pageIndices.IsEmpty)
            {
                return;
            }

            int[] indices = [.. pageIndices];
            if (!Import(source, index, (destination, from, at) => PdfiumNative.FPDF_ImportPagesByIndex(destination, from, indices, (uint) indices.Length, at)))
            {
                throw new PdfiumException("Failed to import pages (invalid indices?)");
            }
        }
    }

    // Caller must hold PdfiumNative.Sync. Runs import, which copies pages from source's native
    // document into this one's at the given index, and keeps the page count and open pages
    // right whatever it did.
    bool Import(PdfiumDocument source, int? index, Func<IntPtr, IntPtr, int, bool> import)
    {
        var doc = Handle;
        var src = source.Handle;
        // PDFium does not clamp this, as it does for a new page; it fails with a misleading
        // message instead.
        if (index is < 0 || index > pageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, $"Page index must be in the range [0, {pageCount}]");
        }

        source.GeneratePendingContent();
        var at = index ?? pageCount;
        var before = pageCount;
        try
        {
            if (ReferenceEquals(source, this) && at < pageCount)
            {
                // PDFium inserts each page before it reads the next, so pages taken from this
                // document ahead of themselves would move under it: it copied the wrong page,
                // or aborted the process. A copy of the document holds still.
                using var copy = Load(Save());
                return import(doc, copy.Handle, at);
            }

            return import(doc, src, at);
        }
        finally
        {
            RefreshPageCount();
            ShiftOpenPages(at, pageCount - before);
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
