public class TextTests
{
    [Test]
    public async Task ExtractText()
    {
        using var document = PdfiumDocument.Load("sample.pdf");
        using var page = document.LoadPage(0);
        var text = page.GetText();
        await Assert.That(text).IsNotNull();
        await Assert.That(page.CharCount).IsGreaterThan(0);
    }

    [Test]
    public async Task CharGeometry()
    {
        using var document = PdfiumDocument.Load("sample.pdf");
        using var page = document.LoadPage(0);
        var chars = page.GetChars();
        await Assert.That(chars.Count).IsGreaterThan(0);
        await Assert.That(chars[0].FontSize).IsGreaterThan(0);
    }
}
