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
        var png = document.RenderPage(0, new RenderOptions
        {
            Grayscale = true,
            Dpi = 150
        });

        #endregion

        await Assert.That(png.Length).IsGreaterThan(0);
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
    public async Task EditPageObjects()
    {
        #region EditPageObjects

        using var document = PdfiumDocument.Load("sample.pdf");
        using (var page = document.LoadPage(0))
        {
            // Draw vector content.
            page.AddLine(new(72, 72), new(520, 72), new(0, 0, 0, 255), width: 2);
            page.AddPath(
                [new(100, 400), new(200, 520), new(300, 400)],
                fill: new(240, 220, 120, 255),
                stroke: new(0, 0, 0, 255));

            // Stamp an image (top-down RGBA pixels) into a rectangle in page points.
            var logo = new byte[16 * 16 * 4];
            Array.Fill(logo, (byte) 255);
            page.AddImage(logo, 16, 16, new(430, 690, 540, 760));

            // Tweak an existing object: recolor and nudge it.
            page.SetObjectFillColor(0, new(20, 20, 120, 255));
            page.MoveObject(0, dx: 4, dy: 0);
        }

        var edited = document.Save();

        #endregion

        await Assert.That(edited.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task SetFileId()
    {
        #region SetFileId

        using var document = PdfiumDocument.Load("sample.pdf");
        // Pin a specific trailer /ID. PDFium has no /ID setter and randomises the changing
        // element on every save, so this is applied by appending an incremental-update trailer.
        byte[] id =
        [
            0xDE, 0xAD, 0xBE, 0xEF, 0x00, 0x11, 0x22, 0x33,
            0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xAA, 0xBB
        ];
        document.SetFileIdentifier(id);
        var pinned = document.Save();

        #endregion

        using var reloaded = PdfiumDocument.Load(pinned);
        await Assert.That(reloaded.GetFileIdentifier()).IsEqualTo(Convert.ToHexStringLower(id));
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
