using System.Diagnostics.CodeAnalysis;

public class Samples
{
    [Test]
    public async Task RenderPage()
    {
        #region RenderPage

        using var document = PdfiumDocument.Load("sample.pdf");
        var png = document.RenderPage(0, dpi: 96);

        #endregion

        await Verify(png, "png");
    }

    [Test]
    [SuppressMessage("Style", "IDE0007:Use implicit type")]
    // ReSharper disable SuggestVarOrType_Elsewhere
    public async Task RenderPages()
    {
        #region RenderPages

        using var document = PdfiumDocument.Load("multi-page.pdf");
        List<byte[]> pages = document.RenderPages();

        #endregion

        var targets = pages
            .Select((_, index) => new Target("png", new MemoryStream(_), $"page_{index + 1:0000}"));
        await Verify(targets);
    }
    // ReSharper restore SuggestVarOrType_Elsewhere


    [Test]
    public async Task DocumentInfo()
    {
        #region DocumentInfo

        using var document = PdfiumDocument.Load("multi-page.pdf");
        Console.WriteLine(document.PageCount);
        Console.WriteLine(document.GetPageSizes());
        Console.WriteLine(document.GetProperties());

        #endregion

        await Verify(
            new
            {
                document.PageCount,
                Sizes = document.GetPageSizes(),
                Properties = document.GetProperties()
            });
    }

    [Test]
    public async Task ExtractText()
    {
        #region ExtractText

        using var document = PdfiumDocument.Load("sample.pdf");
        using var page = document.LoadPage(0);
        var text = page.GetText();

        #endregion

        await Assert.That(text).IsNotNull();
    }

    [Test]
    public async Task SearchText()
    {
        #region SearchText

        using var document = PdfiumDocument.Load("sample.pdf");
        using var page = document.LoadPage(0);
        var matches = page.Search("paragraph");
        // Map a match back to rectangles on the page (e.g. for highlighting).
        var first = matches[0];
        var rectangles = page.GetTextRectangles(first.CharIndex, first.CharCount);

        #endregion

        await Assert.That(matches.Count).IsGreaterThan(0);
        await Assert.That(rectangles).IsNotNull();
    }

    [Test]
    public async Task Bookmarks()
    {
        #region Bookmarks

        using var document = PdfiumDocument.Load("multi-page.pdf");
        foreach (var bookmark in document.GetBookmarks())
        {
            Console.WriteLine($"{bookmark.Title} -> page {bookmark.Destination?.PageIndex}");
        }

        #endregion

        await Assert.That(document.GetBookmarks()).IsNotNull();
    }

    [Test]
    public async Task Annotations()
    {
        #region Annotations

        using var document = PdfiumDocument.Load("sample.pdf");
        using var page = document.LoadPage(0);
        foreach (var annotation in page.GetAnnotations())
        {
            Console.WriteLine($"{annotation.Type}: {annotation.Contents}");
        }

        #endregion

        await Assert.That(page.GetAnnotations()).IsNotNull();
    }

    [Test]
    public async Task FormFields()
    {
        #region FormFields

        using var document = PdfiumDocument.Load("sample.pdf");
        using var form = document.LoadForm();
        using var page = document.LoadPage(0);
        foreach (var field in form.GetFields(page))
        {
            Console.WriteLine($"{field.Name} ({field.Type}) = {field.Value}");
        }

        #endregion

        await Assert.That(form).IsNotNull();
    }

    [Test]
    public async Task RenderGrayscale()
    {
        #region RenderGrayscale

        using var document = PdfiumDocument.Load("sample.pdf");
        var png = document.RenderPage(
            0,
            new RenderOptions
            {
                Grayscale = true,
                Dpi = 150
            });

        #endregion

        await Assert.That(png.Length).IsGreaterThan(0);
    }

    // Sets the process-wide default, so nothing else may render while it runs.
    [Test]
    [NotInParallel]
    public async Task CompressionLevel()
    {
        try
        {
            #region CompressionLevel

            using var document = PdfiumDocument.Load("sample.pdf");
            // For one render; Optimal (zlib level 6) is the default
            var png = document.RenderPage(
                0,
                new RenderOptions
                {
                    CompressionLevel = PngCompressionLevel.Level4
                });

            // For every render not given a level of its own
            RenderOptions.DefaultCompressionLevel = PngCompressionLevel.Level4;

            #endregion

            await Assert.That(png.Length).IsGreaterThan(0);
        }
        finally
        {
            RenderOptions.DefaultCompressionLevel = PngCompressionLevel.Optimal;
        }
    }

    [Test]
    public async Task ProcessPages()
    {
        #region ProcessPages

        using var document = PdfiumDocument.Load("multi-page.pdf");
        // Pages are spread across cores: PDFium works on one at a time, the PNG encoding overlaps
        var pages = document.ProcessPages(_ => (Text: _.GetText(), Png: _.Render(dpi: 96)));

        #endregion

        await Assert.That(pages.Count).IsEqualTo(4);
    }

    [Test]
    public async Task RenderOpenPage()
    {
        #region RenderOpenPage

        using var document = PdfiumDocument.Load("sample.pdf");
        using var page = document.LoadPage(0);
        var text = page.GetText();
        // Renders the page that is already loaded, rather than parsing it again
        var png = page.Render(dpi: 96);

        #endregion

        await Assert.That(text).IsNotNull();
        await Assert.That(png.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task RenderHtml()
    {
        #region RenderHtml

        using var document = PdfiumDocument.Load("multi-page.pdf");
        // Each page's image with its text selectable over it; the images are embedded
        var html = document.RenderHtml();

        #endregion

        await Assert.That(html).StartsWith("<!DOCTYPE html>");
    }

    [Test]
    public async Task RenderHtmlOptions()
    {
        var directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            #region RenderHtmlOptions

            using var document = PdfiumDocument.Load("multi-page.pdf");
            var html = document.RenderHtml(
                new HtmlOptions
                {
                    Render = new()
                    {
                        Dpi = 150
                    },
                    // Write each page's image beside the HTML rather than embedding it
                    ImageUrl = (index, png) =>
                    {
                        var name = $"page{index + 1}.png";
                        File.WriteAllBytes(Path.Combine(directory, name), png);
                        return name;
                    }
                });
            File.WriteAllText(Path.Combine(directory, "document.html"), html);

            #endregion

            await Assert.That(File.Exists(Path.Combine(directory, "page4.png"))).IsTrue();
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Test]
    public async Task RenderTextHtml()
    {
        #region RenderTextHtml

        using var document = PdfiumDocument.Load("sample.pdf");
        // Text-only HTML: the text in its own color and style, with no page image
        var fragment = document.RenderPageHtml(
            0,
            new HtmlOptions
            {
                IncludeImage = false,
                Text = HtmlTextMode.Visible
            });

        #endregion

        await Assert.That(fragment).StartsWith("<div class=\"pdf-page\"");
    }

    [Test]
    public async Task RenderHtmlVisibleText()
    {
        #region RenderHtmlVisibleText

        using var document = PdfiumDocument.Load("multi-page.pdf");
        // Visible text over the page image rendered without its text: images and drawings
        // stay, and the text is sharp at any zoom
        var html = document.RenderHtml(
            new HtmlOptions
            {
                Text = HtmlTextMode.Visible
            });

        #endregion

        await Assert.That(html).Contains("<img ");
    }

    [Test]
    public async Task RenderHtmlUntrustedLinks()
    {
        #region RenderHtmlUntrustedLinks

        using var document = PdfiumDocument.Load("multi-page.pdf");
        // Only web and mail links, and links between the document's pages
        var html = document.RenderHtml(
            new HtmlOptions
            {
                AllowedLinkSchemes = ["http", "https", "mailto"],
                AllowRelativeLinks = false
            });

        #endregion

        await Assert.That(html).Contains("<img ");
    }

    [Test]
    public async Task RenderRegion()
    {
        #region RenderRegion

        using var document = PdfiumDocument.Load("sample.pdf");
        // A clip rectangle in page points (origin bottom-left): the top-left quadrant.
        var clip = new PdfRectangle(0, 396, 306, 792);
        var png = document.RenderRegion(
            0,
            clip,
            new()
            {
                Dpi = 96
            });

        #endregion

        await Assert.That(png.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task MergeDocuments()
    {
        #region MergeDocuments

        using var merged = PdfiumDocument.CreateNew();
        using (var first = PdfiumDocument.Load("sample.pdf"))
        using (var second = PdfiumDocument.Load("multi-page.pdf"))
        {
            merged.ImportPages(first);
            merged.ImportPages(second, "1-2");
        }

        var bytes = merged.Save();

        #endregion

        await Assert.That(merged.PageCount).IsEqualTo(3);
        await Assert.That(bytes.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task EditAndSave()
    {
        #region EditAndSave

        using var document = PdfiumDocument.Load("sample.pdf");
        using (var page = document.LoadPage(0))
        {
            page.AddRectangle(new(40, 700, 240, 760), fill: new(220, 230, 250, 255));
            page.AddText("Stamped", 50, 720, fontSize: 24);
        }

        var stamped = document.Save();

        #endregion

        await Assert.That(stamped.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task Attachments()
    {
        #region Attachments

        using var document = PdfiumDocument.Load("sample.pdf");
        document.AddAttachment("notes.txt", [.. "embedded data"u8]);
        var withAttachment = document.Save();

        #endregion

        await Assert.That(withAttachment.Length).IsGreaterThan(0);
    }
}
