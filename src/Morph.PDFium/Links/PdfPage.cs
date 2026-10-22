namespace Morph.PDFium;

public sealed partial class PdfPage
{
    /// <summary>
    /// The link annotations declared on the page, each with its rectangle and destination or
    /// action. Use <see cref="GetWebLinks"/> for URLs implicitly embedded in the page text.
    /// </summary>
    public IReadOnlyList<PdfLink> GetLinks()
    {
        lock (PdfiumNative.Sync)
        {
            var page = ValidHandle();
            var doc = DocumentHandle;
            var links = new List<PdfLink>();
            var position = 0;
            while (PdfiumNative.FPDFLink_Enumerate(page, ref position, out var link) && link != IntPtr.Zero)
            {
                // A link may name any two opposite corners of its rectangle, and PDFium reports
                // them as written, so they are put in order.
                var rectangle = PdfiumNative.FPDFLink_GetAnnotRect(link, out var rect)
                    ? new PdfRectangle(Math.Min(rect.Left, rect.Right), Math.Min(rect.Bottom, rect.Top), Math.Max(rect.Left, rect.Right), Math.Max(rect.Bottom, rect.Top))
                    : default;
                var (destination, action) = Navigation.ReadTarget(doc, PdfiumNative.FPDFLink_GetDest(doc, link), PdfiumNative.FPDFLink_GetAction(link));
                links.Add(new(rectangle, destination, action));
            }

            return links;
        }
    }

    /// <summary>The URLs implicitly detected in the page's text, each with the rectangles it covers.</summary>
    public IReadOnlyList<PdfWebLink> GetWebLinks()
    {
        lock (PdfiumNative.Sync)
        {
            ValidHandle();
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
                var url = Interop.Utf16ByUnits(units - 1, (linkPage, index), static (state, buffer, length) => PdfiumNative.FPDFLink_GetURL(state.linkPage, state.index, buffer, length));
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
