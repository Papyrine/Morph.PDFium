public class TextTests
{
    [Test]
    public async Task ExtractText()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        using var page = document.LoadPage(0);
        var text = page.GetText();
        await Assert.That(text).IsNotNull();
        await Assert.That(page.CharCount).IsGreaterThan(0);
    }

    [Test]
    public async Task ExtractTextRange()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
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
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        using var page = document.LoadPage(0);
        await Assert.That(() => page.GetText(-1, 5)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task TextQueriesSeeTextAddedAfterTheyWereFirstUsed()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        using var page = document.LoadPage(0);
        // Load the page's text, and the web links derived from it, before editing.
        var charsBefore = page.CharCount;
        await Assert.That(page.Search("ZZQXJ").Count).IsEqualTo(0);
        _ = page.GetWebLinks();

        page.AddText("ZZQXJ https://example.com/added", 72, 72);

        await Assert.That(page.GetText()).Contains("ZZQXJ");
        await Assert.That(page.Search("ZZQXJ").Count).IsEqualTo(1);
        await Assert.That(page.CharCount).IsGreaterThan(charsBefore);
        await Assert.That(page.GetWebLinks().Select(_ => _.Url)).Contains("https://example.com/added");
    }

    [Test]
    public async Task CharGeometry()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        using var page = document.LoadPage(0);
        var chars = page.GetChars();
        await Assert.That(chars.Count).IsGreaterThan(0);
        await Assert.That(chars[0].FontSize).IsGreaterThan(0);
        await Assert.That(chars[0].CodePoint).IsEqualTo(chars[0].Char);
    }
}
