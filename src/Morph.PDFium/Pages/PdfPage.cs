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
    // Set when an edit adds page objects. Regenerating the content stream costs time
    // proportional to the whole page, so it is deferred until something reads the stream
    // rather than repeated for every object added.
    bool contentPending;

    internal PdfPage(PdfiumDocument document, IntPtr handle, int index)
    {
        this.document = document;
        this.handle = handle;
        Index = index;
        document.Track(this);
    }

    /// <summary>The zero-based index of this page within its document.</summary>
    public int Index { get; }

    /// <summary>Page dimensions in points, accounting for the page's rotation.</summary>
    public PageSize Size
    {
        get
        {
            lock (PdfiumNative.Sync)
            {
                var page = ValidHandle();
                return new(PdfiumNative.FPDF_GetPageWidthF(page), PdfiumNative.FPDF_GetPageHeightF(page));
            }
        }
    }

    /// <summary>
    /// The page bounding box (the intersection of its media box and crop box) in points.
    /// </summary>
    public PdfRectangle GetBoundingBox()
    {
        lock (PdfiumNative.Sync)
        {
            var page = ValidHandle();
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

    // Caller must hold PdfiumNative.Sync. Closes the text and web-link handles if loaded.
    // Both are a snapshot of the page's text as it was when they were loaded, so an edit
    // that changes the text calls this and the next text query loads them afresh.
    void CloseText()
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
    }

    public void Dispose()
    {
        // Checked and cleared under the lock so a call on another thread either completes
        // first or sees the page as disposed.
        lock (PdfiumNative.Sync)
        {
            if (handle == IntPtr.Zero)
            {
                return;
            }

            // Last chance to write out deferred edits. Dispose must not throw, so a failure
            // here is not reported; call GenerateContent first to observe one.
            if (contentPending)
            {
                PdfiumNative.FPDFPage_GenerateContent(handle);
                contentPending = false;
            }

            CloseText();
            PdfiumNative.FPDF_ClosePage(handle);
            document.Untrack(this);
            handle = IntPtr.Zero;
        }
    }
}
