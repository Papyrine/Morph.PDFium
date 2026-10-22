namespace Morph.PDFium;

/// <summary>
/// Options for rendering to HTML. Each page becomes a box the size of the page holding its
/// rendered image, with the text and links laid over it in an inline SVG, so the HTML looks
/// like the page while its text stays selectable and searchable and its links work. The
/// defaults give exactly that: the image at 96 DPI, overlay text, links and, for a whole
/// document, its outline.
/// </summary>
public sealed record HtmlOptions
{
    /// <summary>How each page's image is rendered: its dpi, background, grayscale and so on.</summary>
    public RenderOptions Render { get; init; } = new();

    /// <summary>
    /// Whether each page includes its rendered image. Without it only the text (see
    /// <see cref="Text"/>) and links remain, and the page's images and drawings are lost. With
    /// <see cref="HtmlTextMode.Visible"/> text, the image is rendered without the page's text,
    /// which the HTML draws over it instead.
    /// </summary>
    public bool IncludeImage { get; init; } = true;

    /// <summary>How the page's text is included. Defaults to <see cref="HtmlTextMode.Overlay"/>.</summary>
    public HtmlTextMode Text { get; init; } = HtmlTextMode.Overlay;

    /// <summary>
    /// Whether link annotations, and the URLs PDFium detects in the page text, become
    /// clickable areas. A link to a page of the same document points at that page's id (see
    /// <see cref="PageIdPrefix"/>). Only http, https, mailto, ftp and tel links and relative
    /// references are kept: a javascript: link, a launch action or a link into an embedded
    /// file is left out.
    /// </summary>
    public bool IncludeLinks { get; init; } = true;

    /// <summary>
    /// Whether a whole document's HTML starts with its outline (bookmarks) as a nested list
    /// of links, when it has one.
    /// </summary>
    public bool IncludeOutline { get; init; } = true;

    /// <summary>
    /// The title of a whole document's HTML. Defaults to the document's Title property, or
    /// "Document" when it has none.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// The start of each page's id, followed by its one-based page number: "page-1",
    /// "page-2" and so on by default. Set it to tell apart the pages of several documents on
    /// one HTML page.
    /// </summary>
    public string PageIdPrefix { get; init; } = "page-";

    /// <summary>
    /// Given a page's zero-based index and its rendered PNG, returns the URL for the page's
    /// image, for example after writing the PNG to a file beside the HTML. When null, the
    /// default, each image is embedded as a data: URI, which makes the HTML self-contained
    /// but about a third larger than the images. Rendering a whole document calls this from
    /// several threads at once.
    /// </summary>
    public Func<int, byte[], string>? ImageUrl { get; init; }

    /// <summary>
    /// Whether the images of pages after the first load only as they are scrolled near
    /// (<c>loading="lazy"</c>), so a long document opens quickly. On by default. Turn it off
    /// for HTML that is printed or captured without being scrolled through, in a browser that
    /// does not load lazy images first.
    /// </summary>
    public bool LazyLoadImages { get; init; } = true;

    /// <summary>
    /// The most pages a whole document's HTML holds: only its first this many pages are
    /// rendered, and the document itself is left as it is. Null, the default, renders every
    /// page. Only the pages rendered get a printed page size, and a bookmark, or a link on a
    /// page, that leads to a page past the limit is kept as plain text or left out, rather than
    /// pointing at a page id the HTML does not have. A single page renders whatever this is,
    /// though its links to pages past the limit are left out too.
    /// </summary>
    public int? MaxPages { get; init; }

    internal void Validate(string paramName)
    {
        if (MaxPages is <= 0)
        {
            throw new ArgumentOutOfRangeException(paramName, MaxPages, "MaxPages must be positive");
        }

        if (Render is null)
        {
            throw new ArgumentException("Render must not be null", paramName);
        }

        if (PageIdPrefix is null)
        {
            throw new ArgumentException("PageIdPrefix must not be null", paramName);
        }

        if (!Enum.IsDefined(Text))
        {
            throw new ArgumentOutOfRangeException(paramName, Text, "Text is not a defined HtmlTextMode");
        }

        Render.Validate(paramName);
    }
}
