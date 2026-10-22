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
            GeneratePendingContent();
            var page = PdfiumNative.FPDF_LoadPage(doc, index);
            if (page == IntPtr.Zero)
            {
                throw new PdfiumException($"Failed to load page {index}");
            }

            return new(this, page, index);
        }
    }

    /// <summary>
    /// Loads every page and passes it to <paramref name="process"/>, with the pages spread across
    /// the processor's cores, and returns the results in page order. PDFium itself still works on
    /// one page at a time, but whatever else <paramref name="process"/> does, such as the PNG
    /// encoding inside <see cref="PdfPage.Render(double)"/> or handling the text it extracts, runs
    /// alongside the other pages' native work. For a whole document that is where most of the time
    /// goes, so this is the fastest way to convert one.
    /// <paramref name="process"/> is called on several threads at once: it must not share state
    /// between calls without synchronising, and must not keep the page, which is disposed once it
    /// returns.
    /// </summary>
    public IReadOnlyList<T> ProcessPages<T>(Func<PdfPage, T> process)
    {
        ThrowIfDisposed();
        return ProcessPages(Enumerable.Range(0, pageCount), process);
    }

    /// <summary>
    /// <see cref="ProcessPages{T}(Func{PdfPage,T})"/> for only the pages at
    /// <paramref name="indices"/>, with the results in that order.
    /// </summary>
    public IReadOnlyList<T> ProcessPages<T>(IEnumerable<int> indices, Func<PdfPage, T> process)
    {
        ArgumentNullException.ThrowIfNull(indices);
        ArgumentNullException.ThrowIfNull(process);
        ThrowIfDisposed();
        var pages = indices.ToArray();
        foreach (var index in pages)
        {
            ValidateIndex(index);
        }

        var results = new T[pages.Length];
        // Each worker holds one page, and whatever process allocates for it, at a time, so the
        // degree of parallelism also bounds the memory in use.
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount
        };
        try
        {
            Parallel.For(0, pages.Length, options, _ =>
            {
                using var page = LoadPage(pages[_]);
                results[_] = process(page);
            });
        }
        catch (AggregateException exception)
        {
            // Surface what process threw, as a sequential loop would.
            ExceptionDispatchInfo.Capture(exception.InnerExceptions[0]).Throw();
            throw;
        }

        return results;
    }
}
