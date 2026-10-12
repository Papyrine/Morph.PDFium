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

    // A dpi this high is only reachable with a tiny clip, but it must still give a valid PNG.
    [Test]
    public async Task PhysicalDimensionsStayWithinPngLimit()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        var options = new RenderOptions
        {
            Dpi = 100_000_000
        };
        var png = document.RenderRegion(0, new(0, 0, 0.01, 0.01), options);

        // The pHYs chunk follows the 8 byte signature and the 25 byte IHDR chunk.
        await Assert.That(png.AsSpan(37, 4).SequenceEqual("pHYs"u8)).IsTrue();
        await Assert.That(BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(41))).IsEqualTo((uint) int.MaxValue);
        await Assert.That(BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(45))).IsEqualTo((uint) int.MaxValue);
    }

    // One line of text on a page whose media box does not start at the origin, and the same
    // on a page rotated a quarter turn. On both, page coordinates and the displayed page
    // differ, which they do not on the sample files.
    static byte[] PdfWithOffsetAndRotatedPages() =>
        CraftedPdf.Build(new()
        {
            [1] = "<< /Type /Catalog /Pages 2 0 R >>",
            [2] = "<< /Type /Pages /Kids [3 0 R 5 0 R] /Count 2 >>",
            [3] = "<< /Type /Page /Parent 2 0 R /MediaBox [100 200 500 700] /Contents 4 0 R /Resources << /Font << /F1 7 0 R >> >> >>",
            [4] = CraftedPdf.Stream("BT /F1 24 Tf 120 660 Td (OFFSET) Tj ET"),
            [5] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Rotate 90 /Contents 6 0 R /Resources << /Font << /F1 7 0 R >> >> >>",
            [6] = CraftedPdf.Stream("BT /F1 24 Tf 72 700 Td (ROTATED) Tj ET"),
            [7] = CraftedPdf.Helvetica
        });

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    public async Task RenderRegionTakesPageCoordinates(int index)
    {
        using var document = PdfiumDocument.Load(PdfWithOffsetAndRotatedPages());
        using var page = document.LoadPage(index);
        // The rectangle the text API reports for the page's only line of text.
        var text = page.GetTextRectangles().Single();
        var options = new RenderOptions
        {
            Dpi = 144
        };

        var onText = document.RenderRegion(index, text, options);
        // The same size of rectangle, moved well clear of the text.
        var offText = document.RenderRegion(index, new(text.Left, text.Bottom - 100, text.Right, text.Top - 100), options);

        // A blank region deflates to almost nothing, so content shows up as extra bytes.
        await Assert.That(ReadPngSize(onText)).IsEqualTo(ReadPngSize(offText));
        await Assert.That(onText.Length).IsGreaterThan(offText.Length * 3);
    }

    [Test]
    public async Task RenderRegionOnRotatedPageSwapsWidthAndHeight()
    {
        using var document = PdfiumDocument.Load(PdfWithOffsetAndRotatedPages());
        // 200 x 50 points in page coordinates, on a page displayed a quarter turn round.
        var png = document.RenderRegion(
            1,
            new(50, 600, 250, 650),
            new()
            {
                Dpi = 72
            });
        await Assert.That(ReadPngSize(png)).IsEqualTo((50, 200));
    }

    static (int width, int height) ReadPngSize(byte[] png)
    {
        var width = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16));
        var height = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20));
        return (width, height);
    }
}
