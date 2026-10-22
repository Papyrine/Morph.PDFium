// Rendering through an open PdfPage must give exactly what rendering by index through the
// document gives.
public class PageRenderTests
{
    [Test]
    public async Task RenderMatchesDocumentRender()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.multi_page_pdf);
        var options = new RenderOptions
        {
            Dpi = 50,
            Grayscale = true,
            Background = new(0, 0, 255, 255)
        };
        for (var index = 0; index < document.PageCount; index++)
        {
            using var page = document.LoadPage(index);
            await Assert.That(page.Render()).IsEquivalentTo(document.RenderPage(index));
            await Assert.That(page.Render(options)).IsEquivalentTo(document.RenderPage(index, options));
        }
    }

    // Pages whose page coordinates differ from the displayed page, so the size and the clip
    // mapping both matter.
    [Test]
    public async Task RenderRegionMatchesDocumentRenderRegion()
    {
        using var document = PdfiumDocument.Load(FormRenderTests.PdfWithOffsetAndRotatedPages());
        for (var index = 0; index < document.PageCount; index++)
        {
            using var page = document.LoadPage(index);
            var text = page.GetTextRectangles().Single();
            await Assert.That(page.Render()).IsEquivalentTo(document.RenderPage(index));
            await Assert.That(page.RenderRegion(text)).IsEquivalentTo(document.RenderRegion(index, text));
        }
    }

    [Test]
    public async Task InvalidArgumentsThrow()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        using var page = document.LoadPage(0);
        await Assert.That(() => page.Render(0)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => page.Render(new RenderOptions { Dpi = double.NaN })).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => page.RenderRegion(new(0, 0, 0, 100))).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => page.Render(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task DisposedPageThrows()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        var page = document.LoadPage(0);
        page.Dispose();
        await Assert.That(() => page.Render()).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task DisposedDocumentThrows()
    {
        var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        var page = document.LoadPage(0);
        document.Dispose();
        await Assert.That(() => page.Render()).Throws<ObjectDisposedException>();
    }
}
