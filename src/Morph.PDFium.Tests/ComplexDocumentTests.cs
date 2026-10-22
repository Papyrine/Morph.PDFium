/// <summary>
/// Real documents, as against the plain text of sample.pdf and the one-feature PDFs built in
/// the other tests: IRS Form 1040, dense with ruled tables and form fields, and a NASA fact
/// sheet with a photo, wrapped text and vertical text. See test-assets.md for where they came
/// from.
/// </summary>
public class ComplexDocumentTests
{
    public static IEnumerable<string> Documents() =>
        ["irs-f1040", "nasa-training-for-space"];

    static string PathOf(string name) =>
        name switch
        {
            "irs-f1040" => ProjectFiles.irs_f1040_pdf,
            "nasa-training-for-space" => ProjectFiles.nasa_training_for_space_pdf,
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
        };

    [Test]
    [MethodDataSource(nameof(Documents))]
    public async Task RenderPages(string name)
    {
        using var document = PdfiumDocument.Load(PathOf(name));
        var targets = document.RenderPages()
            .Select((_, index) => new Target("png", new MemoryStream(_), $"page_{index + 1}"));
        await Verify(targets)
            .UseParameters(name);
    }

    // Visible text over each page rendered without its text, with the images named rather than
    // embedded so the HTML stays readable. The HTML and every page's background are verified.
    [Test]
    [MethodDataSource(nameof(Documents))]
    public async Task RenderHtml(string name)
    {
        using var document = PdfiumDocument.Load(PathOf(name));
        var backgrounds = new byte[document.PageCount][];
        var html = document.RenderHtml(
            new()
            {
                Text = HtmlTextMode.Visible,
                ImageUrl = (index, png) =>
                {
                    backgrounds[index] = png;
                    return $"background_{index + 1}.png";
                }
            });
        List<Target> targets = [new("html", html)];
        targets.AddRange(backgrounds.Select((_, index) => new Target("png", new MemoryStream(_), $"background_{index + 1}")));
        await Verify(targets)
            .UseParameters(name);
    }

    // The overlay's text layer: every run of text, where it sits and what it says. Kept apart
    // from the HTML above, whose styles would otherwise hide a change in placement.
    [Test]
    [MethodDataSource(nameof(Documents))]
    public async Task OverlayText(string name)
    {
        using var document = PdfiumDocument.Load(PathOf(name));
        var html = document.RenderPageHtml(
            0,
            new()
            {
                ImageUrl = (_, _) => "page.png"
            });
        await Verify(html, "html")
            .UseParameters(name);
    }
}
