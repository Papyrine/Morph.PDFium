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

    // How many pages ProcessPagesInOrder lets its workers get ahead of the next page to consume.
    internal static int PageWindow => Environment.ProcessorCount * 2;

    /// <summary>
    /// Like <see cref="ProcessPages{T}(Func{PdfPage,T})"/>, but hands each page's result to
    /// <paramref name="consume"/> in page order as soon as it and every page before it are
    /// done, instead of collecting them all, so only a window of results is held at once.
    /// <paramref name="consume"/> runs on the worker threads, one call at a time. Only the
    /// first <paramref name="count"/> pages are processed.
    /// </summary>
    internal void ProcessPagesInOrder<T>(int count, Func<PdfPage, T> process, Action<T> consume)
    {
        ThrowIfDisposed();
        count = Math.Min(count, pageCount);
        var workers = Math.Min(Environment.ProcessorCount, count);
        // Pages are handed out in order, and a worker takes one only while fewer than
        // PageWindow are started but not yet consumed. So the results waiting on a slow page
        // stay within the window, rather than every later page piling up behind it.
        using var window = new SemaphoreSlim(PageWindow);
        var results = new T[count];
        var done = new bool[count];
        var gate = new Lock();
        // Guarded by gate: the next page to consume.
        var next = 0;
        var taken = -1;
        var failed = false;
        try
        {
            Parallel.For(0, workers, _ =>
            {
                while (true)
                {
                    window.Wait();
                    var index = Interlocked.Increment(ref taken);
                    if (index >= count || Volatile.Read(ref failed))
                    {
                        window.Release();
                        return;
                    }

                    try
                    {
                        T result;
                        using (var page = LoadPage(index))
                        {
                            result = process(page);
                        }

                        lock (gate)
                        {
                            results[index] = result;
                            done[index] = true;
                            // Once anything has failed, consume is not called again: a writer
                            // that threw is not handed the pages after the one it failed on.
                            while (next < count && done[next] && !failed)
                            {
                                var ready = results[next];
                                results[next] = default!;
                                next++;
                                try
                                {
                                    consume(ready);
                                }
                                catch
                                {
                                    // Set before the lock is released, so no other worker
                                    // drains past it in the meantime.
                                    failed = true;
                                    throw;
                                }

                                window.Release();
                            }
                        }
                    }
                    catch
                    {
                        // The failed page is never consumed, so the window would never open
                        // again: wake every waiting worker to see the failure and stop.
                        Volatile.Write(ref failed, true);
                        window.Release(workers);
                        throw;
                    }
                }
            });
        }
        catch (AggregateException exception)
        {
            // Surface what process or consume threw, as a sequential loop would.
            ExceptionDispatchInfo.Capture(exception.InnerExceptions[0]).Throw();
            throw;
        }
    }
}
