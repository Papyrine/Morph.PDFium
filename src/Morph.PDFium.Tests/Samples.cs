public class Samples
{
    #region RenderPage

    [Test]
    public async Task RenderPage()
    {
        using var document = PdfiumDocument.Load("sample.pdf");
        var png = document.RenderPage(0, dpi: 96);
        await Verify(png, "png");
    }

    #endregion

    #region RenderPages

    [Test]
    public async Task RenderPages()
    {
        using var document = PdfiumDocument.Load("multi-page.pdf");
        var targets = document.RenderPages()
            .Select((_, index) => new Target("png", new MemoryStream(_), $"page_{index + 1:0000}"))
            .ToList();
        await Verify(targets);
    }

    #endregion

    #region DocumentInfo

    [Test]
    public async Task DocumentInfo()
    {
        using var document = PdfiumDocument.Load("multi-page.pdf");
        await Verify(
            new
            {
                document.PageCount,
                Sizes = document.GetPageSizes(),
                Properties = document.GetProperties()
            });
    }

    #endregion
}
