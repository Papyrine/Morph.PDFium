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
    public async Task ExtractTextRange()
    {
        using var document = PdfiumDocument.Load("sample.pdf");
        using var page = document.LoadPage(0);
        var all = page.GetText()!;

        await Assert.That(page.GetText(0, 5)).IsEqualTo(all[..5]);
        await Assert.That(page.GetText(2, -1)).IsEqualTo(all[2..]);
        await Assert.That(page.GetText(0, all.Length + 100)).IsEqualTo(all);
        await Assert.That(page.GetText(0, 0)).IsNull();
        await Assert.That(page.GetText(page.CharCount, 5)).IsNull();
    }

    [Test]
    public async Task ExtractTextRangeNegativeStartThrows()
    {
        using var document = PdfiumDocument.Load("sample.pdf");
        using var page = document.LoadPage(0);
        await Assert.That(() => page.GetText(-1, 5)).Throws<ArgumentOutOfRangeException>();
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
