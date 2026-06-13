public class FormRenderTests
{
    [Test]
    public async Task FormTypeIsNoneForPlainPdf()
    {
        using var document = PdfiumDocument.Load("sample.pdf");
        await Assert.That(document.GetFormType()).IsEqualTo(FormType.None);
    }

    [Test]
    public async Task LoadFormAndRender()
    {
        using var document = PdfiumDocument.Load("sample.pdf");
        using var form = document.LoadForm();
        using var page = document.LoadPage(0);
        var fields = form.GetFields(page);
        await Assert.That(fields).IsNotNull();

        var png = form.RenderPage(0);
        await Assert.That(png.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task RenderGrayscale()
    {
        using var document = PdfiumDocument.Load("sample.pdf");
        var png = document.RenderPage(0, new RenderOptions
        {
            Grayscale = true,
            Dpi = 72
        });
        await Assert.That(png.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task RenderRegion()
    {
        using var document = PdfiumDocument.Load("sample.pdf");
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

    static (int width, int height) ReadPngSize(byte[] png)
    {
        var width = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16));
        var height = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20));
        return (width, height);
    }
}
