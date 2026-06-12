namespace Morph.PDFium;

/// <summary>A link annotation on a page: a clickable rectangle with a destination or action.</summary>
/// <param name="Rectangle">The link's bounding rectangle in page points.</param>
/// <param name="Destination">The view destination, when the link targets one directly.</param>
/// <param name="Action">The action, when the link carries one instead of a destination.</param>
public readonly record struct PdfLink(PdfRectangle Rectangle, PdfDestination? Destination, PdfAction? Action);

/// <summary>
/// A URL implicitly detected in the page's text (e.g. "https://example.com"), independent of
/// any link annotations the PDF may declare.
/// </summary>
/// <param name="Url">The detected URL.</param>
/// <param name="Rectangles">The rectangles the URL text occupies, in page points.</param>
public sealed record PdfWebLink(string Url, IReadOnlyList<PdfRectangle> Rectangles);

public sealed partial class PdfPage
{
    /// <summary>
    /// The link annotations declared on the page, each with its rectangle and destination or
    /// action. Use <see cref="GetWebLinks"/> for URLs implicitly embedded in the page text.
    /// </summary>
    public IReadOnlyList<PdfLink> GetLinks()
    {
        var page = ValidHandle();
        var doc = DocumentHandle;
        lock (PdfiumNative.Sync)
        {
            var links = new List<PdfLink>();
            var position = 0;
            while (PdfiumNative.FPDFLink_Enumerate(page, ref position, out var link) && link != IntPtr.Zero)
            {
                var rectangle = PdfiumNative.FPDFLink_GetAnnotRect(link, out var rect)
                    ? new PdfRectangle(rect.Left, rect.Bottom, rect.Right, rect.Top)
                    : default;
                var destination = Navigation.ReadDestination(doc, PdfiumNative.FPDFLink_GetDest(doc, link));
                var action = destination is null ? Navigation.ReadAction(doc, PdfiumNative.FPDFLink_GetAction(link)) : null;
                links.Add(new(rectangle, destination, action));
            }

            return links;
        }
    }

    /// <summary>The URLs implicitly detected in the page's text, each with the rectangles it covers.</summary>
    public IReadOnlyList<PdfWebLink> GetWebLinks()
    {
        var page = ValidHandle();
        lock (PdfiumNative.Sync)
        {
            _ = page;
            var linkPage = WebLinkHandle();
            var count = PdfiumNative.FPDFLink_CountWebLinks(linkPage);
            if (count <= 0)
            {
                return [];
            }

            var result = new List<PdfWebLink>(count);
            for (var index = 0; index < count; index++)
            {
                var units = PdfiumNative.FPDFLink_GetURL(linkPage, index, [], 0);
                var url = Interop.Utf16ByUnits(units - 1, (buffer, length) => PdfiumNative.FPDFLink_GetURL(linkPage, index, buffer[..length], length));
                if (url is null)
                {
                    continue;
                }

                var rectCount = PdfiumNative.FPDFLink_CountRects(linkPage, index);
                var rectangles = new List<PdfRectangle>(Math.Max(0, rectCount));
                for (var rectIndex = 0; rectIndex < rectCount; rectIndex++)
                {
                    if (PdfiumNative.FPDFLink_GetRect(linkPage, index, rectIndex, out var left, out var top, out var right, out var bottom))
                    {
                        rectangles.Add(new(left, bottom, right, top));
                    }
                }

                result.Add(new(url, rectangles));
            }

            return result;
        }
    }

    // Caller must hold PdfiumNative.Sync. Lazily loads the web-link index over the text page.
    IntPtr WebLinkHandle()
    {
        if (webLinkHandle == IntPtr.Zero)
        {
            webLinkHandle = PdfiumNative.FPDFLink_LoadWebLinks(TextHandle());
            if (webLinkHandle == IntPtr.Zero)
            {
                throw new PdfiumException($"Failed to load web links for page {Index}");
            }
        }

        return webLinkHandle;
    }
}
