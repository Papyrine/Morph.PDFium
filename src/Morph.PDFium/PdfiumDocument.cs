namespace Morph.PDFium;

/// <summary>
/// A PDF document loaded into PDFium, exposing page metadata and PNG rendering.
/// PDFium is not thread safe, so all native calls are serialized on a process-wide
/// lock; instances may be shared across threads.
/// </summary>
public sealed partial class PdfiumDocument :
    IDisposable
{
    // PDFium reads from the source buffer on demand for the lifetime of the
    // document, so the managed array stays pinned until Dispose.
    GCHandle pinnedBytes;
    IntPtr handle;
    // Not readonly: page import/insert/delete change the count, so mutators refresh it.
    int pageCount;
    // Pages and form sessions still open on this document. PDFium requires them to be
    // closed before the document, so Dispose closes whatever the caller left open.
    List<IDisposable> children = [];

    PdfiumDocument(GCHandle pinnedBytes, IntPtr handle, int pageCount)
    {
        this.pinnedBytes = pinnedBytes;
        this.handle = handle;
        this.pageCount = pageCount;
    }

    public static PdfiumDocument Load(string path, string? password = null) =>
        Load(File.ReadAllBytes(path), password);

    public static PdfiumDocument Load(Stream stream, string? password = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        // A seekable stream reports how much is left, so read it straight into an array of
        // exactly that size instead of growing a buffer and then copying out of it.
        if (stream.CanSeek)
        {
            // A position beyond the end leaves nothing to read, the same as being at the end.
            var remaining = Math.Max(0, stream.Length - stream.Position);
            var bytes = GC.AllocateUninitializedArray<byte>(checked((int) remaining));
            stream.ReadExactly(bytes);
            return Load(bytes, password);
        }

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return Load(buffer.ToArray(), password);
    }

    public static PdfiumDocument Load(byte[] bytes, string? password = null)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        var pinned = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            IntPtr document;
            var pages = 0;
            uint error = 0;
            lock (PdfiumNative.Sync)
            {
                document = LoadDocument(pinned.AddrOfPinnedObject(), bytes.Length, password);
                if (document == IntPtr.Zero)
                {
                    error = PdfiumNative.FPDF_GetLastError();
                }
                else
                {
                    pages = PdfiumNative.FPDF_GetPageCount(document);
                }
            }

            if (document == IntPtr.Zero)
            {
                throw new PdfiumException($"Failed to load PDF: {MapLoadError(error)}");
            }

            var result = new PdfiumDocument(pinned, document, pages);
            // Ownership of the pinned buffer transferred to the document
            pinned = default;
            return result;
        }
        finally
        {
            if (pinned.IsAllocated)
            {
                pinned.Free();
            }
        }
    }

    static IntPtr LoadDocument(IntPtr data, int length, string? password)
    {
        if (password == null)
        {
            return PdfiumNative.FPDF_LoadMemDocument(data, length, IntPtr.Zero);
        }

        // PDFium expects a null terminated UTF-8 (or Latin-1) password. It is only
        // read during load, so the pin is scoped to the call.
        var passwordBytes = Encoding.UTF8.GetBytes(password + '\0');
        var pinned = GCHandle.Alloc(passwordBytes, GCHandleType.Pinned);
        try
        {
            return PdfiumNative.FPDF_LoadMemDocument(data, length, pinned.AddrOfPinnedObject());
        }
        finally
        {
            pinned.Free();
        }
    }

    public int PageCount
    {
        get
        {
            ThrowIfDisposed();
            return pageCount;
        }
    }

    /// <summary>Page dimensions in PDF points (1/72 inch).</summary>
    public PageSize GetPageSize(int index)
    {
        lock (PdfiumNative.Sync)
        {
            ThrowIfDisposed();
            ValidateIndex(index);
            if (PdfiumNative.FPDF_GetPageSizeByIndexF(handle, index, out var size) == 0)
            {
                throw new PdfiumException($"Failed to read size of page {index}");
            }

            return new(size.Width, size.Height);
        }
    }

    public IReadOnlyList<PageSize> GetPageSizes()
    {
        ThrowIfDisposed();
        var sizes = new List<PageSize>(pageCount);
        for (var index = 0; index < pageCount; index++)
        {
            sizes.Add(GetPageSize(index));
        }

        return sizes;
    }

    static readonly (string Name, byte[] Tag)[] metadataTags =
    [
        ("Title", [.. "Title\0"u8]),
        ("Author", [.. "Author\0"u8]),
        ("Subject", [.. "Subject\0"u8]),
        ("Keywords", [.. "Keywords\0"u8]),
        ("Creator", [.. "Creator\0"u8]),
        ("Producer", [.. "Producer\0"u8]),
        ("CreationDate", [.. "CreationDate\0"u8]),
        ("ModDate", [.. "ModDate\0"u8])
    ];

    /// <summary>Document information dictionary entries that have a value.</summary>
    public Dictionary<string, string>? GetProperties()
    {
        Dictionary<string, string>? properties = null;
        lock (PdfiumNative.Sync)
        {
            ThrowIfDisposed();
            foreach (var (name, tag) in metadataTags)
            {
                // Returns the required buffer size in bytes for the UTF-16LE value
                // including the two byte terminator; 2 or less means no value.
                var length = PdfiumNative.FPDF_GetMetaText(handle, tag, [], 0);
                if (length <= 2)
                {
                    continue;
                }

                var buffer = new byte[length];
                PdfiumNative.FPDF_GetMetaText(handle, tag, buffer, length);
                var value = Encoding.Unicode.GetString(buffer, 0, (int) length - 2);
                if (value.Length == 0)
                {
                    continue;
                }

                properties ??= [];
                properties[name] = value;
            }
        }

        return properties;
    }

    /// <summary>
    /// Renders a page to a PNG image: white background, annotations included,
    /// dimensions derived from the page size and <paramref name="dpi"/>.
    /// </summary>
    public byte[] RenderPage(int index, double dpi = 96)
    {
        ThrowIfDisposed();
        ValidateIndex(index);
        RenderOptions.ValidateDpi(dpi, nameof(dpi));

        using var rendered = RenderPixels(index, dpi, PdfiumNative.RenderAnnotations, OpaqueWhite, null);
        // Encode outside the PDFium lock: it is pure managed work
        return PngEncoder.Encode(rendered.Pixels, rendered.Width, rendered.Height, dpi, RenderOptions.DefaultCompressionLevel);
    }

    /// <summary>
    /// Renders every page as <see cref="RenderPage(int,double)"/> does, in parallel through
    /// <see cref="ProcessPages{T}(Func{PdfPage,T})"/>.
    /// </summary>
    public List<byte[]> RenderPages(double dpi = 96)
    {
        RenderOptions.ValidateDpi(dpi, nameof(dpi));
        return [.. ProcessPages(_ => _.Render(dpi))];
    }

    // Byte-order agnostic: every channel is 0xFF.
    internal const uint OpaqueWhite = 0xFFFFFFFF;

    // Shared rasteriser. renderFlags carries caller options (annotations, grayscale, ...);
    // ReverseByteOrder is always added so PDFium emits RGBA matching the PNG encoder. background
    // must already be in the bitmap's byte order (see RenderOptions.ToFillColor). When clip is
    // null the whole page is rendered; otherwise only that rectangle, given in page coordinates.
    // The pixels come back in a pooled buffer, so the caller disposes the result once it is
    // done with them.
    internal RenderedPixels RenderPixels(int index, double dpi, int renderFlags, uint background, PdfRectangle? clip, IntPtr formHandle = 0)
    {
        lock (PdfiumNative.Sync)
        {
            // Callers validate up front, but only a check made under the lock is safe
            // against a Dispose on another thread.
            ThrowIfDisposed();
            GeneratePendingContent();
            if (PdfiumNative.FPDF_GetPageSizeByIndexF(handle, index, out var size) == 0)
            {
                throw new PdfiumException($"Failed to read size of page {index}");
            }

            var page = PdfiumNative.FPDF_LoadPage(handle, index);
            if (page == IntPtr.Zero)
            {
                throw new PdfiumException($"Failed to load page {index}");
            }

            // Notify the form environment so widget appearances are current before drawing.
            if (formHandle != IntPtr.Zero)
            {
                PdfiumNative.FORM_OnAfterLoadPage(page, formHandle);
            }

            try
            {
                return RenderLoadedPage(page, size, index, dpi, renderFlags | PdfiumNative.ReverseByteOrder, background, clip, formHandle);
            }
            finally
            {
                if (formHandle != IntPtr.Zero)
                {
                    PdfiumNative.FORM_OnBeforeClosePage(page, formHandle);
                }

                PdfiumNative.FPDF_ClosePage(page);
            }
        }
    }

    // Caller must hold PdfiumNative.Sync.
    internal static RenderedPixels RenderLoadedPage(IntPtr page, PdfiumNative.PageSizeF size, int index, double dpi, int flags, uint background, PdfRectangle? clip, IntPtr formHandle)
    {
        // The part of the displayed page to draw, in points from its top-left corner.
        var area = (Left: 0d, Top: 0d, Width: (double) size.Width, Height: (double) size.Height);
        if (clip is { } rectangle)
        {
            area = ToDisplayArea(page, size, rectangle, index);
        }

        var width = ToPixels(area.Width, dpi);
        var height = ToPixels(area.Height, dpi);
        // Size in 64 bit: a wrapped 32 bit product would give PDFium a buffer smaller than
        // the bitmap it describes, and the render would write past the end of it.
        // Compare the pixel count rather than the byte count: at the largest dimensions
        // even a 64 bit byte count wraps, whereas width * height always fits.
        var pixelCount = (long) width * height;
        if (pixelCount > Array.MaxLength / 4)
        {
            throw new ArgumentOutOfRangeException(nameof(dpi), dpi, $"A {width}x{height} bitmap for page {index} exceeds the maximum buffer size");
        }

        var stride = width * 4;
        // Rented, not allocated: a page's pixels run to megabytes, and a fresh array that size
        // costs page faults to first touch and a gen2 collection to reclaim, every page. It is
        // not cleared: FPDFBitmap_FillRect below overwrites every byte before anything reads it.
        var pixels = ArrayPool<byte>.Shared.Rent((int) (pixelCount * 4));
        var pinned = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            var bitmap = PdfiumNative.FPDFBitmap_CreateEx(
                width,
                height,
                PdfiumNative.FormatBgra,
                pinned.AddrOfPinnedObject(),
                stride);
            if (bitmap == IntPtr.Zero)
            {
                throw new PdfiumException($"Failed to allocate {width}x{height} bitmap for page {index}");
            }

            try
            {
                // The buffer is uninitialised, so a failed fill must not go on to be encoded.
                if (PdfiumNative.FPDFBitmap_FillRect(bitmap, 0, 0, width, height, background) == 0)
                {
                    throw new PdfiumException($"Failed to fill {width}x{height} bitmap for page {index}");
                }

                if (clip is null)
                {
                    PdfiumNative.FPDF_RenderPageBitmap(bitmap, page, 0, 0, width, height, 0, flags);
                    // Draw interactive form fields (widgets) on top of the page content.
                    if (formHandle != IntPtr.Zero)
                    {
                        PdfiumNative.FPDF_FFLDraw(formHandle, bitmap, page, 0, 0, width, height, 0, flags);
                    }
                }
                else
                {
                    // PDFium applies this matrix after its own display matrix, so the input is
                    // the displayed page: points from its top-left corner with y running down.
                    // Only a scale and a shift of the area's corner to the bitmap origin are
                    // needed.
                    var scaleX = width / area.Width;
                    var scaleY = height / area.Height;
                    var matrix = new FsMatrix
                    {
                        A = (float) scaleX,
                        D = (float) scaleY,
                        E = (float) (-area.Left * scaleX),
                        F = (float) (-area.Top * scaleY)
                    };
                    var clipping = new FsRectF { Left = 0, Top = 0, Right = width, Bottom = height };
                    PdfiumNative.FPDF_RenderPageBitmapWithMatrix(bitmap, page, in matrix, in clipping, flags);
                }
            }
            finally
            {
                PdfiumNative.FPDFBitmap_Destroy(bitmap);
            }
        }
        catch
        {
            // The caller never sees the buffer, so it goes back to the pool here.
            ArrayPool<byte>.Shared.Return(pixels);
            throw;
        }
        finally
        {
            pinned.Free();
        }

        return new(pixels, width, height);
    }

    // Caller must hold PdfiumNative.Sync. Converts a rectangle in page coordinates, the
    // space text, link and annotation rectangles are reported in, to the area it covers on
    // the displayed page. The two differ when the page is rotated or when its media or crop
    // box does not start at the origin, and a quarter turn swaps the width and height.
    static (double Left, double Top, double Width, double Height) ToDisplayArea(IntPtr page, PdfiumNative.PageSizeF size, PdfRectangle clip, int index)
    {
        // FPDF_PageToDevice answers in whole device units, so ask about a device this many
        // times the page's size in points to keep precision below a point.
        const int unitsPerPoint = 1000;
        var deviceWidth = (int) Math.Round(size.Width * unitsPerPoint);
        var deviceHeight = (int) Math.Round(size.Height * unitsPerPoint);
        if (!PdfiumNative.FPDF_PageToDevice(page, 0, 0, deviceWidth, deviceHeight, 0, clip.Left, clip.Top, out var x1, out var y1) ||
            !PdfiumNative.FPDF_PageToDevice(page, 0, 0, deviceWidth, deviceHeight, 0, clip.Right, clip.Bottom, out var x2, out var y2))
        {
            throw new PdfiumException($"Failed to map the clip rectangle onto page {index}");
        }

        // At least one device unit each way, so the scale computed from it stays finite.
        var width = Math.Max(1, Math.Abs((long) x2 - x1));
        var height = Math.Max(1, Math.Abs((long) y2 - y1));
        return (
            (double) Math.Min(x1, x2) / unitsPerPoint,
            (double) Math.Min(y1, y2) / unitsPerPoint,
            (double) width / unitsPerPoint,
            (double) height / unitsPerPoint);
    }

    static int ToPixels(double points, double dpi) =>
        Math.Max(1, (int) Math.Round(points * dpi / 72d));

    static string MapLoadError(uint error) =>
        error switch
        {
            1 => "unknown error",
            2 => "file not found or could not be opened",
            3 => "file is not a PDF or is corrupt",
            4 => "password required or incorrect password",
            5 => "unsupported security scheme",
            6 => "page not found or content error",
            _ => $"error code {error}"
        };

    internal void ValidateIndex(int index)
    {
        if (index < 0 || index >= pageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, $"Page index must be in the range [0, {pageCount - 1}]");
        }
    }

    void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(handle == IntPtr.Zero, this);

    // Re-reads the page count after a mutation. Caller must hold PdfiumNative.Sync.
    internal void RefreshPageCount() =>
        pageCount = PdfiumNative.FPDF_GetPageCount(handle);

    /// <summary>The native FPDF_DOCUMENT handle, validated for disposal. For use by page/feature partials.</summary>
    internal IntPtr Handle
    {
        get
        {
            ThrowIfDisposed();
            return handle;
        }
    }

    // Caller must hold PdfiumNative.Sync. Page edits defer regenerating their content
    // stream, so anything that reads a page's stored content (save, render, import, loading
    // the page again) calls this first.
    internal void GeneratePendingContent()
    {
        foreach (var child in children)
        {
            if (child is PdfPage page)
            {
                page.GeneratePendingContent();
            }
        }
    }

    internal void Track(IDisposable child)
    {
        lock (PdfiumNative.Sync)
        {
            children.Add(child);
        }
    }

    internal void Untrack(IDisposable child)
    {
        lock (PdfiumNative.Sync)
        {
            children.Remove(child);
        }
    }

    // Safety net for a document that is never disposed. Without it the source bytes would
    // stay pinned, and the native document allocated, for the rest of the process. The pages
    // and form sessions it closes are plain managed objects with no finalizers of their own,
    // so they are still intact when this runs.
    ~PdfiumDocument() =>
        Dispose();

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        // Checked and cleared under the lock so a call on another thread either completes
        // first or sees the document as disposed.
        lock (PdfiumNative.Sync)
        {
            if (handle == IntPtr.Zero)
            {
                return;
            }

            // Each child untracks itself as it is disposed, hence the copy. Pages go first
            // so none outlives a form session.
            IDisposable[] open = [.. children];
            foreach (var child in open)
            {
                if (child is PdfPage)
                {
                    child.Dispose();
                }
            }

            foreach (var child in open)
            {
                child.Dispose();
            }

            PdfiumNative.FPDF_CloseDocument(handle);
            handle = IntPtr.Zero;
            // Documents created in memory (CreateNew) have no pinned source buffer.
            if (pinnedBytes.IsAllocated)
            {
                pinnedBytes.Free();
            }
        }
    }
}
