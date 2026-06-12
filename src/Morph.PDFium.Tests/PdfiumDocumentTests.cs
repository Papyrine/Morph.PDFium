public class PdfiumDocumentTests
{
    [Test]
    public async Task PageCount()
    {
        using var document = PdfiumDocument.Load("multi-page.pdf");
        await Assert.That(document.PageCount).IsEqualTo(4);
    }

    [Test]
    public async Task LoadFromStream()
    {
        await using var stream = File.OpenRead("sample.pdf");
        using var document = PdfiumDocument.Load(stream);
        await Assert.That(document.PageCount).IsEqualTo(1);
    }

    [Test]
    public async Task LoadFromBytes()
    {
        using var document = PdfiumDocument.Load(await File.ReadAllBytesAsync("sample.pdf"));
        await Assert.That(document.PageCount).IsEqualTo(1);
    }

    [Test]
    public async Task LetterAt96Dpi()
    {
        using var document = PdfiumDocument.Load("sample.pdf");
        var (width, height) = ReadPngSize(document.RenderPage(0));
        // US Letter is 612 x 792 points
        await Assert.That(width).IsEqualTo(816);
        await Assert.That(height).IsEqualTo(1056);
    }

    [Test]
    public async Task DpiScalesOutput()
    {
        using var document = PdfiumDocument.Load("sample.pdf");
        var (width, height) = ReadPngSize(document.RenderPage(0, dpi: 192));
        await Assert.That(width).IsEqualTo(1632);
        await Assert.That(height).IsEqualTo(2112);
    }

    [Test]
    public async Task RenderIsDeterministic()
    {
        using var document = PdfiumDocument.Load("sample.pdf");
        var first = document.RenderPage(0);
        var second = document.RenderPage(0);
        await Assert.That(second).IsEquivalentTo(first);
    }

    [Test]
    public async Task ConcurrentRenders()
    {
        using var document = PdfiumDocument.Load("multi-page.pdf");
        var reference = document.RenderPage(0);
        var renders = await Task.WhenAll(
            Enumerable.Range(0, 8)
                .Select(_ => Task.Run(() => document.RenderPage(0))));
        foreach (var render in renders)
        {
            await Assert.That(render).IsEquivalentTo(reference);
        }
    }

    [Test]
    public async Task Properties()
    {
        using var document = PdfiumDocument.Load("sample.pdf");
        var properties = document.GetProperties();
        await Assert.That(properties).IsNotNull();
        await Assert.That(properties!.ContainsKey("Producer")).IsTrue();
    }

    [Test]
    public async Task InvalidPdfThrows()
    {
        var exception = await Assert.That(() => PdfiumDocument.Load("not a pdf"u8.ToArray()))
            .Throws<PdfiumException>();
        await Assert.That(exception!.Message).Contains("not a PDF");
    }

    [Test]
    public async Task PageIndexOutOfRangeThrows() =>
        await Assert.That(() =>
            {
                using var document = PdfiumDocument.Load("sample.pdf");
                document.RenderPage(5);
            })
            .Throws<ArgumentOutOfRangeException>();

    [Test]
    public async Task DisposedThrows()
    {
        var document = PdfiumDocument.Load("sample.pdf");
        document.Dispose();
        await Assert.That(() => document.RenderPage(0)).Throws<ObjectDisposedException>();
    }

    static (int width, int height) ReadPngSize(byte[] png)
    {
        // IHDR data starts at offset 16: 8 byte signature + 4 byte length + 4 byte type
        var width = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16));
        var height = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20));
        return (width, height);
    }
}
