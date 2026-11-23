// RenderOptions.MaxPixelDimension lowers the dpi of a page that would otherwise render larger
// than the cap, and leaves pages that fit alone, through every entry point that takes options.
public class MaxPixelDimensionTests
{
    // Pages 0 and 2 are 8000 by 4500 points, landscape then portrait, the size of the slides in
    // a real report whose pages rendered 16667 pixels wide at 150 dpi, past WebP's 16383. Page
    // 1 is US Letter.
    static byte[] OversizedPdf()
    {
        var content = CraftedPdf.Stream("0 0 1 rg 100 100 1000 1000 re f");
        return CraftedPdf.Build(new()
        {
            [1] = "<< /Type /Catalog /Pages 2 0 R >>",
            [2] = "<< /Type /Pages /Kids [3 0 R 4 0 R 5 0 R] /Count 3 >>",
            [3] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 8000 4500] /Contents 6 0 R >>",
            [4] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 6 0 R >>",
            [5] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 4500 8000] /Contents 6 0 R >>",
            [6] = content
        });
    }

    static RenderOptions Capped => new() { Dpi = 150, MaxPixelDimension = 4096 };

    // The dpi at which 8000 points is 4096 pixels.
    const double cappedDpi = 4096 * 72d / 8000;

    [Test]
    public async Task OversizedPageIsScaledToFit()
    {
        using var document = PdfiumDocument.Load(OversizedPdf());
        var png = document.RenderPage(0, Capped);
        await Assert.That(ReadPngSize(png)).IsEqualTo((4096, 2304));
        await Assert.That(ReadPixelsPerMeter(png)).IsEqualTo(PixelsPerMeter(cappedDpi));
        // The same as rendering at the lower dpi to begin with.
        await Bytes.AreEqual(png, document.RenderPage(0, new RenderOptions { Dpi = cappedDpi }));
    }

    [Test]
    public async Task PortraitPageIsCappedOnItsHeight()
    {
        using var document = PdfiumDocument.Load(OversizedPdf());
        await Assert.That(ReadPngSize(document.RenderPage(2, Capped))).IsEqualTo((2304, 4096));
    }

    [Test]
    public async Task PageThatFitsKeepsItsDpi()
    {
        using var document = PdfiumDocument.Load(OversizedPdf());
        var png = document.RenderPage(1, Capped);
        await Assert.That(ReadPngSize(png)).IsEqualTo((1275, 1650));
        await Bytes.AreEqual(png, document.RenderPage(1, new RenderOptions { Dpi = 150 }));
    }

    [Test]
    public async Task PageExactlyAtTheCapKeepsItsDpi()
    {
        using var document = PdfiumDocument.Load(OversizedPdf());
        // At 72 dpi Letter is 612 by 792 pixels.
        var options = new RenderOptions { Dpi = 72, MaxPixelDimension = 792 };
        var png = document.RenderPage(1, options);
        await Assert.That(ReadPngSize(png)).IsEqualTo((612, 792));
        await Assert.That(ReadPixelsPerMeter(png)).IsEqualTo(PixelsPerMeter(72));
    }

    [Test]
    public async Task EveryEntryPointHonoursTheCap()
    {
        using var document = PdfiumDocument.Load(OversizedPdf());
        var expected = document.RenderPage(0, Capped);
        using (var page = document.LoadPage(0))
        {
            await Bytes.AreEqual(page.Render(Capped), expected);
        }

        var pages = document.RenderPages(Capped);
        await Bytes.AreEqual(pages[0], expected);
        await Assert.That(ReadPngSize(pages[1])).IsEqualTo((1275, 1650));
        await Assert.That(ReadPngSize(pages[2])).IsEqualTo((2304, 4096));

        using var form = document.LoadForm();
        await Assert.That(ReadPngSize(form.RenderPage(0, Capped))).IsEqualTo((4096, 2304));
    }

    [Test]
    public async Task RegionIsCappedOnItsOwnSize()
    {
        using var document = PdfiumDocument.Load(OversizedPdf());
        // 4000 by 1000 points: 8333 by 2083 pixels at 150 dpi, so the cap halves it, about.
        var clip = new PdfRectangle(0, 1000, 4000, 2000);
        var png = document.RenderRegion(0, clip, Capped);
        await Assert.That(ReadPngSize(png)).IsEqualTo((4096, 1024));
        using var page = document.LoadPage(0);
        await Bytes.AreEqual(page.RenderRegion(clip, Capped), png);
    }

    [Test]
    public async Task HtmlPixelsReportTheDpiUsed()
    {
        using var document = PdfiumDocument.Load(OversizedPdf());
        var images = new ConcurrentDictionary<int, (int Width, int Height, double Dpi)>();
        var html = document.RenderHtml(
            new()
            {
                Render = Capped,
                ImageUrlFromPixels = (index, image) =>
                {
                    images[index] = (image.Width, image.Height, image.Dpi);
                    return $"images/{index + 1}.webp";
                }
            });
        await Assert.That(images[0]).IsEqualTo((4096, 2304, cappedDpi));
        await Assert.That(images[1]).IsEqualTo((1275, 1650, 150d));
        await Assert.That(images[2]).IsEqualTo((2304, 4096, cappedDpi));
        // The page is still laid out at its size in points, the image stretched over it.
        await Assert.That(html).Contains("width:8000pt;max-width:100%;aspect-ratio:8000/4500");
    }

    [Test]
    public async Task HtmlEmbeddedPngIsCapped()
    {
        using var document = PdfiumDocument.Load(OversizedPdf());
        var pngs = new ConcurrentDictionary<int, byte[]>();
        document.RenderHtml(
            new()
            {
                Render = Capped,
                ImageUrl = (index, png) =>
                {
                    pngs[index] = png;
                    return $"images/{index + 1}.png";
                }
            });
        await Bytes.AreEqual(pngs[0], document.RenderPage(0, Capped));
    }

    [Test]
    public async Task NonPositiveCapThrows()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        using var page = document.LoadPage(0);
        using var form = document.LoadForm();
        foreach (var max in new[] { 0, -1 })
        {
            var options = new RenderOptions { MaxPixelDimension = max };
            await Assert.That(() => document.RenderPage(0, options)).Throws<ArgumentOutOfRangeException>();
            await Assert.That(() => document.RenderPages(options)).Throws<ArgumentOutOfRangeException>();
            await Assert.That(() => document.RenderRegion(0, new(0, 0, 100, 100), options)).Throws<ArgumentOutOfRangeException>();
            await Assert.That(() => page.Render(options)).Throws<ArgumentOutOfRangeException>();
            await Assert.That(() => page.RenderRegion(new(0, 0, 100, 100), options)).Throws<ArgumentOutOfRangeException>();
            await Assert.That(() => form.RenderPage(0, options)).Throws<ArgumentOutOfRangeException>();
            await Assert.That(() => document.RenderHtml(new() { Render = options })).Throws<ArgumentOutOfRangeException>();
        }
    }

    static (int width, int height) ReadPngSize(byte[] png)
    {
        // IHDR data starts at offset 16: 8 byte signature + 4 byte length + 4 byte type
        var width = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16));
        var height = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20));
        return (width, height);
    }

    // The x pixels per metre of the pHYs chunk, which follows its four type bytes.
    static uint ReadPixelsPerMeter(byte[] png)
    {
        var type = png.AsSpan().IndexOf("pHYs"u8);
        return BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(type + 4));
    }

    static uint PixelsPerMeter(double dpi) =>
        (uint) Math.Round(dpi / 0.0254);
}
