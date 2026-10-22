public class ProcessPagesTests
{
    [Test]
    public async Task ResultsAreInPageOrder()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.multi_page_pdf);
        await Assert.That(string.Join(",", document.ProcessPages(_ => _.Index))).IsEqualTo("0,1,2,3");
        await Assert.That(string.Join(",", document.ProcessPages([3, 0, 2], _ => _.Index))).IsEqualTo("3,0,2");
    }

    [Test]
    public async Task MatchesSequentialConversion()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.multi_page_pdf);
        var parallel = document.ProcessPages(_ => (Text: _.GetText(), Png: _.Render()));
        await Assert.That(parallel.Count).IsEqualTo(document.PageCount);
        for (var index = 0; index < document.PageCount; index++)
        {
            using var page = document.LoadPage(index);
            await Assert.That(parallel[index].Text).IsEqualTo(page.GetText());
            await Assert.That(parallel[index].Png).IsEquivalentTo(page.Render());
        }
    }

    [Test]
    public async Task PageIsDisposedAfterProcessing()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        var page = document.ProcessPages(_ => _).Single();
        await Assert.That(page.GetText).Throws<ObjectDisposedException>();
    }

    // Whatever the callback throws comes out as itself, not wrapped in an AggregateException.
    [Test]
    public async Task ExceptionsSurfaceAsThrown()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.multi_page_pdf);
        await Assert.That(() => document.ProcessPages<int>(_ => throw new InvalidOperationException("from the callback")))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task InvalidArgumentsThrow()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.multi_page_pdf);
        await Assert.That(() => document.ProcessPages([4], _ => _.Index)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => document.ProcessPages([-1], _ => _.Index)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => document.ProcessPages<int>(null!)).Throws<ArgumentNullException>();
        await Assert.That(() => document.ProcessPages(null!, _ => _.Index)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task DisposedDocumentThrows()
    {
        var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        document.Dispose();
        await Assert.That(() => document.ProcessPages(_ => _.Index)).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task RenderPagesWithOptionsMatchesRenderPage()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.multi_page_pdf);
        var options = new RenderOptions
        {
            Dpi = 50,
            Grayscale = true
        };
        var pages = document.RenderPages(options);
        await Assert.That(pages.Count).IsEqualTo(document.PageCount);
        for (var index = 0; index < pages.Count; index++)
        {
            await Assert.That(pages[index]).IsEquivalentTo(document.RenderPage(index, options));
        }
    }
}
