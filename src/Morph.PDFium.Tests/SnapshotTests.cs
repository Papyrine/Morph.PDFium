public class SnapshotTests
{
    [Test]
    public async Task ExtractedText()
    {
        using var document = PdfiumDocument.Load("sample.pdf");
        using var page = document.LoadPage(0);
        await Verify(page.GetText());
    }

    [Test]
    public async Task DocumentInfo()
    {
        using var document = PdfiumDocument.Load("multi-page.pdf");
        await Verify(
            new
            {
                Version = document.GetPdfVersion(),
                Permissions = document.GetPermissions(),
                PageMode = document.GetPageDisplayMode(),
                Form = document.GetFormType(),
                document.SignatureCount,
                document.AttachmentCount
            });
    }

    [Test]
    public async Task CharGeometry()
    {
        using var document = PdfiumDocument.Load("sample.pdf");
        using var page = document.LoadPage(0);
        // Snapshot the first few characters' Unicode, box and font size.
        await Verify(page.GetChars().Take(5));
    }

    [Test]
    public async Task GrayscaleRender()
    {
        using var document = PdfiumDocument.Load("sample.pdf");
        var png = document.RenderPage(0, new RenderOptions
        {
            Grayscale = true,
            Dpi = 96
        });
        await Verify(png, "png");
    }

    [Test]
    public async Task BackgroundRender()
    {
        using var document = PdfiumDocument.Load("sample.pdf");
        var png = document.RenderPage(0, new RenderOptions
        {
            Background = new(255, 0, 0, 255),
            Dpi = 96
        });
        await Verify(png, "png");
    }

    [Test]
    public async Task RegionRender()
    {
        using var document = PdfiumDocument.Load("sample.pdf");
        // Top-left quadrant of US Letter, in page points.
        var png = document.RenderRegion(
            0,
            new(0, 396, 306, 792),
            new()
            {
                Dpi = 96
            });
        await Verify(png, "png");
    }

    [Test]
    public async Task EditedPageRender()
    {
        using var document = PdfiumDocument.Load("sample.pdf");
        using (var page = document.LoadPage(0))
        {
            page.AddRectangle(new(40, 700, 240, 760), new(220, 230, 250, 255), stroke: new(40, 60, 120, 255));
            page.AddText("Morph.PDFium", 50, 720, fontSize: 24, color: new(20, 40, 100, 255));
        }

        var edited = document.Save();
        using var reloaded = PdfiumDocument.Load(edited);
        await Verify(reloaded.RenderPage(0), "png");
    }
}
