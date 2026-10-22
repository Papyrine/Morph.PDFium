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
    /// <see cref="PageIdPrefix"/>). Only links of the schemes in
    /// <see cref="AllowedLinkSchemes"/> and, with <see cref="AllowRelativeLinks"/>, relative
    /// references are kept: a javascript: link, a launch action or a link into an embedded
    /// file is left out.
    /// </summary>
    public bool IncludeLinks { get; init; } = true;

    /// <summary>
    /// The URL schemes, such as "https", that a link, or a bookmark in the outline, may have
    /// to be kept, compared ignoring case; a link of any other scheme is left out. Defaults to
    /// http, https, mailto, ftp and tel. For a PDF from an untrusted source, narrow it to, say,
    /// http, https and mailto, and turn off <see cref="AllowRelativeLinks"/>. Never add a
    /// scheme that runs script, such as javascript. A link to a page of the same document is
    /// kept whatever this holds.
    /// </summary>
    public IReadOnlyCollection<string> AllowedLinkSchemes { get; init; } = DefaultLinkSchemes;

    /// <summary>
    /// Whether a link, or a bookmark, that is a relative reference, with no scheme, is kept:
    /// a link to a file beside the PDF, say, which the HTML then resolves against its own
    /// location. On by default. A link to a page of the same document is kept either way.
    /// </summary>
    public bool AllowRelativeLinks { get; init; } = true;

    /// <summary>The default <see cref="AllowedLinkSchemes"/>: http, https, mailto, ftp and tel.</summary>
    public static IReadOnlyCollection<string> DefaultLinkSchemes { get; } = ["http", "https", "mailto", "ftp", "tel"];

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

    internal void Validate(string paramName)
    {
        if (Render is null)
        {
            throw new ArgumentException("Render must not be null", paramName);
        }

        if (PageIdPrefix is null)
        {
            throw new ArgumentException("PageIdPrefix must not be null", paramName);
        }

        if (AllowedLinkSchemes is null)
        {
            throw new ArgumentException("AllowedLinkSchemes must not be null", paramName);
        }

        foreach (var scheme in AllowedLinkSchemes)
        {
            if (string.IsNullOrEmpty(scheme))
            {
                throw new ArgumentException("AllowedLinkSchemes must not hold a null or empty scheme", paramName);
            }
        }

        if (!Enum.IsDefined(Text))
        {
            throw new ArgumentOutOfRangeException(paramName, Text, "Text is not a defined HtmlTextMode");
        }

        Render.Validate(paramName);
    }
}
