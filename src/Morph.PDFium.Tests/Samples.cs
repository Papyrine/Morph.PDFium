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
}
