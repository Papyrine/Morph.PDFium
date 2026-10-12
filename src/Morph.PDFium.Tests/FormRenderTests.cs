public class FormRenderTests
{
    [Test]
    public async Task FormTypeIsNoneForPlainPdf()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        await Assert.That(document.GetFormType()).IsEqualTo(FormType.None);
    }

    [Test]
    public async Task LoadFormAndRender()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        using var form = document.LoadForm();
        using var page = document.LoadPage(0);
        var fields = form.GetFields(page);
        await Assert.That(fields).IsNotNull();

        var png = form.RenderPage(0);
        await Assert.That(png.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task FormRenderPageIndexOutOfRangeThrows()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        using var form = document.LoadForm();
        await Assert.That(() => form.RenderPage(5)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => form.RenderPage(-1)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task FormRenderNonPositiveDpiThrows()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        using var form = document.LoadForm();
        var options = new RenderOptions
        {
            Dpi = 0
        };
        await Assert.That(() => form.RenderPage(0, options)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task RenderGrayscale()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        var png = document.RenderPage(
            0,
            new RenderOptions
            {
                Grayscale = true,
                Dpi = 72
            });
        await Assert.That(png.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task RenderRegion()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        // US Letter top-left quadrant in points.
        var png = document.RenderRegion(
            0,
            new(0, 396, 306, 792),
            new()
            {
                Dpi = 96
            });
        var (width, height) = ReadPngSize(png);
        await Assert.That(width).IsEqualTo(408);
        await Assert.That(height).IsEqualTo(528);
    }

    // sample.pdf has a single line of text near the top of the page and nothing below it.
    // The region snapshot is compared by SSIM, which a mostly white image passes even when
    // the text is missing, so check directly that the text lands in the right half.
    [Test]
    public async Task RenderRegionShowsTheRequestedArea()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        var topHalf = document.RenderRegion(0, new(0, 396, 612, 792));
        var bottomHalf = document.RenderRegion(0, new(0, 0, 612, 396));

        // A blank region deflates to almost nothing, so content shows up as extra bytes.
        await Assert.That(topHalf.Length).IsGreaterThan(bottomHalf.Length * 2);
    }

    static (int width, int height) ReadPngSize(byte[] png)
    {
        var width = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16));
        var height = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20));
        return (width, height);
    }
}
