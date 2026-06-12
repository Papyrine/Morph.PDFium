namespace Morph.PDFium;

/// <summary>
/// A single page loaded from a <see cref="PdfiumDocument"/>, exposing page geometry plus
/// text, link and annotation access. Obtain one via <see cref="PdfiumDocument.LoadPage"/>
/// and dispose it when finished; it holds native page resources for its lifetime.
/// Like the rest of the wrapper, every native call is serialised on the process-wide lock.
/// </summary>
public sealed partial class PdfPage :
    IDisposable
{
    readonly PdfiumDocument document;
    IntPtr handle;
    // Text and web-link handles are loaded lazily the first time they are needed and
    // released on Dispose, since most pages are never queried for text.
    IntPtr textHandle;
    IntPtr webLinkHandle;

    internal PdfPage(PdfiumDocument document, IntPtr handle, int index)
    {
        this.document = document;
        this.handle = handle;
        Index = index;
    }

    /// <summary>The zero-based index of this page within its document.</summary>
    public int Index { get; }

    /// <summary>Page dimensions in points, accounting for the page's rotation.</summary>
    public PageSize Size
    {
        get
        {
            var page = ValidHandle();
            lock (PdfiumNative.Sync)
            {
                return new(PdfiumNative.FPDF_GetPageWidthF(page), PdfiumNative.FPDF_GetPageHeightF(page));
            }
        }
    }

    /// <summary>
    /// The page bounding box (the intersection of its media box and crop box) in points.
    /// </summary>
    public PdfRectangle GetBoundingBox()
    {
        var page = ValidHandle();
        lock (PdfiumNative.Sync)
        {
            if (!PdfiumNative.FPDF_GetPageBoundingBox(page, out var rect))
            {
                throw new PdfiumException($"Failed to read bounding box of page {Index}");
            }

            return new(rect.Left, rect.Bottom, rect.Right, rect.Top);
        }
    }

    internal IntPtr ValidHandle()
    {
        ObjectDisposedException.ThrowIf(handle == IntPtr.Zero, this);
        // Touch the document so a disposed document surfaces as ObjectDisposedException too.
        _ = document.Handle;
        return handle;
    }

    /// <summary>The owning document. Some native calls (links, destinations) require its handle.</summary>
    internal IntPtr DocumentHandle => document.Handle;

    // Caller must hold PdfiumNative.Sync. Lazily loads the FPDFText page.
    IntPtr TextHandle()
    {
        if (textHandle == IntPtr.Zero)
        {
            textHandle = PdfiumNative.FPDFText_LoadPage(handle);
            if (textHandle == IntPtr.Zero)
            {
                throw new PdfiumException($"Failed to load text for page {Index}");
            }
        }

        return textHandle;
    }

    public void Dispose()
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        lock (PdfiumNative.Sync)
        {
            if (webLinkHandle != IntPtr.Zero)
            {
                PdfiumNative.FPDFLink_CloseWebLinks(webLinkHandle);
                webLinkHandle = IntPtr.Zero;
            }

            if (textHandle != IntPtr.Zero)
            {
                PdfiumNative.FPDFText_ClosePage(textHandle);
                textHandle = IntPtr.Zero;
            }

            PdfiumNative.FPDF_ClosePage(handle);
        }

        handle = IntPtr.Zero;
    }
}
