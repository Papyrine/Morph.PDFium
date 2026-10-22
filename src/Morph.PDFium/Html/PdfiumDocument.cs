namespace Morph.PDFium;

public sealed partial class PdfiumDocument
{
    /// <summary>
    /// Renders the page at <paramref name="index"/> to an HTML fragment, as
    /// <see cref="PdfPage.RenderHtml"/> does.
    /// </summary>
    public string RenderPageHtml(int index, HtmlOptions? options = null)
    {
        options ??= new();
        options.Validate(nameof(options));
        using var page = LoadPage(index);
        return page.RenderHtmlPage(options, owned: true);
    }

    /// <summary>
    /// Renders the whole document to a self-contained HTML document: its metadata in the head,
    /// its outline as a list of links (see <see cref="HtmlOptions.IncludeOutline"/>), then
    /// every page, or the first <see cref="HtmlOptions.MaxPages"/>, as
    /// <see cref="PdfPage.RenderHtml"/> renders it, one under another, each on a sheet of its
    /// own when printed. The pages are rendered in parallel, as
    /// <see cref="ProcessPages{T}(Func{PdfPage,T})"/> does.
    /// </summary>
    public string RenderHtml(HtmlOptions? options = null)
    {
        using var writer = new StringWriter();
        RenderHtml(writer, options);
        return writer.ToString();
    }

    /// <summary>
    /// Writes the whole document as HTML to <paramref name="writer"/>, as
    /// <see cref="RenderHtml(HtmlOptions?)"/> does. Each page is written as soon as it and the
    /// pages before it are rendered, so only a few pages are held in memory at once, however
    /// long the document. The writer is called from the rendering threads, one call at a time.
    /// </summary>
    public void RenderHtml(TextWriter writer, HtmlOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(writer);
        options ??= new();
        options.Validate(nameof(options));
        var properties = GetProperties();
        var language = GetLanguage();
        IReadOnlyList<PdfBookmark> outline = [];
        if (options.IncludeOutline)
        {
            outline = GetBookmarks();
        }

        var title = options.Title;
        if (title is null && properties is not null)
        {
            properties.TryGetValue("Title", out title);
        }

        // Read without loading the pages, and as they are displayed, rotation included: the
        // same sizes the pages render at. Only the pages rendered need one.
        var count = Math.Min(PageCount, options.MaxPages ?? int.MaxValue);
        var sizes = GetPageSizes(count);
        HtmlWriter.DocumentStart(writer, title ?? "Document", language, properties, outline, sizes, options);
        // Each page is written as soon as it and the pages before it are rendered, so a long
        // document is never held in memory whole.
        ProcessPagesInOrder(count, _ => _.RenderHtmlPage(options, owned: true), _ => writer.Write(_));
        writer.Write(HtmlWriter.DocumentEnd);
    }
}
