namespace Morph.PDFium;

/// <summary>
/// A PDF document loaded into PDFium, exposing page metadata and PNG rendering.
/// PDFium is not thread safe, so all native calls are serialized on a process-wide
/// lock; instances may be shared across threads.
/// </summary>
public sealed class PdfiumDocument :
    IDisposable
{
    // PDFium reads from the source buffer on demand for the lifetime of the
    // document, so the managed array stays pinned until Dispose.
    GCHandle pinnedBytes;
    IntPtr handle;
    readonly int pageCount;

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
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return Load(buffer.ToArray(), password);
    }

    public static PdfiumDocument Load(byte[] bytes, string? password = null)
    {
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
        ThrowIfDisposed();
        ValidateIndex(index);
        lock (PdfiumNative.Sync)
        {
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
        ThrowIfDisposed();
        Dictionary<string, string>? properties = null;
        lock (PdfiumNative.Sync)
        {
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
        if (dpi <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dpi), dpi, "dpi must be positive");
        }

        var (pixels, width, height) = RenderPixels(index, dpi);
        // Encode outside the PDFium lock: it is pure managed work
        return PngEncoder.Encode(pixels, width, height, dpi);
    }

    public List<byte[]> RenderPages(double dpi = 96)
    {
        ThrowIfDisposed();
        var pages = new List<byte[]>(pageCount);
        for (var index = 0; index < pageCount; index++)
        {
            pages.Add(RenderPage(index, dpi));
        }

        return pages;
    }

    (byte[] pixels, int width, int height) RenderPixels(int index, double dpi)
    {
        lock (PdfiumNative.Sync)
        {
            if (PdfiumNative.FPDF_GetPageSizeByIndexF(handle, index, out var size) == 0)
            {
                throw new PdfiumException($"Failed to read size of page {index}");
            }

            var width = ToPixels(size.Width, dpi);
            var height = ToPixels(size.Height, dpi);
            var stride = width * 4;
            var pixels = new byte[stride * height];

            var page = PdfiumNative.FPDF_LoadPage(handle, index);
            if (page == IntPtr.Zero)
            {
                throw new PdfiumException($"Failed to load page {index}");
            }

            try
            {
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
                        // White is byte-order agnostic, so the fill is correct even though
                        // ReverseByteOrder only applies to the page render below (which
                        // makes PDFium emit RGBA instead of BGRA, matching PNG layout).
                        _ = PdfiumNative.FPDFBitmap_FillRect(bitmap, 0, 0, width, height, 0xFFFFFFFF);
                        PdfiumNative.FPDF_RenderPageBitmap(
                            bitmap,
                            page,
                            0,
                            0,
                            width,
                            height,
                            0,
                            PdfiumNative.RenderAnnotations | PdfiumNative.ReverseByteOrder);
                    }
                    finally
                    {
                        PdfiumNative.FPDFBitmap_Destroy(bitmap);
                    }
                }
                finally
                {
                    pinned.Free();
                }
            }
            finally
            {
                PdfiumNative.FPDF_ClosePage(page);
            }

            return (pixels, width, height);
        }
    }

    static int ToPixels(float points, double dpi) =>
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

    void ValidateIndex(int index)
    {
        if (index < 0 || index >= pageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, $"Page index must be in the range [0, {pageCount - 1}]");
        }
    }

    void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(handle == IntPtr.Zero, this);

    public void Dispose()
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        lock (PdfiumNative.Sync)
        {
            PdfiumNative.FPDF_CloseDocument(handle);
        }

        handle = IntPtr.Zero;
        pinnedBytes.Free();
    }
}
