public class PdfiumDocumentTests
{
    [Test]
    public async Task PageCount()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.multi_page_pdf);
        await Assert.That(document.PageCount).IsEqualTo(4);
    }

    [Test]
    public async Task LoadFromStream()
    {
        await using var stream = ProjectFiles.sample_pdf.OpenRead();
        using var document = PdfiumDocument.Load(stream);
        await Assert.That(document.PageCount).IsEqualTo(1);
    }

    [Test]
    public async Task LoadFromStreamReadsFromCurrentPosition()
    {
        var pdf = await File.ReadAllBytesAsync(ProjectFiles.sample_pdf);
        byte[] prefix = [1, 2, 3, 4, 5];
        using var stream = new MemoryStream([.. prefix, .. pdf]);
        stream.Position = prefix.Length;
        using var document = PdfiumDocument.Load(stream);
        await Assert.That(document.PageCount).IsEqualTo(1);
    }

    [Test]
    public async Task LoadFromNonSeekableStream()
    {
        await using var stream = new NonSeekableStream(await File.ReadAllBytesAsync(ProjectFiles.sample_pdf));
        using var document = PdfiumDocument.Load(stream);
        await Assert.That(document.PageCount).IsEqualTo(1);
    }

    class NonSeekableStream(byte[] bytes) :
        MemoryStream(bytes)
    {
        public override bool CanSeek => false;
    }

    [Test]
    public async Task LoadFromBytes()
    {
        using var document = PdfiumDocument.Load(await File.ReadAllBytesAsync(ProjectFiles.sample_pdf));
        await Assert.That(document.PageCount).IsEqualTo(1);
    }

    [Test]
    public async Task LetterAt96Dpi()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        var (width, height) = ReadPngSize(document.RenderPage(0));
        // US Letter is 612 x 792 points
        await Assert.That(width).IsEqualTo(816);
        await Assert.That(height).IsEqualTo(1056);
    }

    [Test]
    public async Task DpiScalesOutput()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        var (width, height) = ReadPngSize(document.RenderPage(0, dpi: 192));
        await Assert.That(width).IsEqualTo(1632);
        await Assert.That(height).IsEqualTo(2112);
    }

    [Test]
    public async Task RenderIsDeterministic()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        var first = document.RenderPage(0);
        var second = document.RenderPage(0);
        await Assert.That(second).IsEquivalentTo(first);
    }

    [Test]
    public async Task ConcurrentRenders()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.multi_page_pdf);
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
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        var properties = document.GetProperties();
        await Assert.That(properties).IsNotNull();
        await Assert.That(properties!.ContainsKey("Producer")).IsTrue();
    }

    [Test]
    public async Task LoadNullThrows()
    {
        await Assert.That(() => PdfiumDocument.Load((byte[]) null!)).Throws<ArgumentNullException>();
        await Assert.That(() => PdfiumDocument.Load((Stream) null!)).Throws<ArgumentNullException>();
        await Assert.That(() => PdfiumDocument.Load((string) null!)).Throws<ArgumentNullException>();
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
                using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
                document.RenderPage(5);
            })
            .Throws<ArgumentOutOfRangeException>();

    // US Letter at 2400 dpi overflows a 32 bit byte count to a negative value; at 3400 dpi it
    // wraps back round to a small positive one.
    [Test]
    [Arguments(2400)]
    [Arguments(3400)]
    [Arguments(1_000_000)]
    public async Task OversizedRenderThrows(double dpi)
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        await Assert.That(() => document.RenderPage(0, dpi)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task OversizedRegionThrows()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        var options = new RenderOptions
        {
            Dpi = 3400
        };
        await Assert.That(() => document.RenderRegion(0, new(0, 0, 612, 792), options))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task DisposedThrows()
    {
        var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        document.Dispose();
        await Assert.That(() => document.RenderPage(0)).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task DisposeDuringUseOnAnotherThread()
    {
        for (var iteration = 0; iteration < 20; iteration++)
        {
            var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
            var reader = Task.Run(() =>
            {
                try
                {
                    while (true)
                    {
                        document.GetPageSize(0);
                    }
                }
                catch (ObjectDisposedException)
                {
                    // The only acceptable way for a call to lose the race with Dispose.
                }
            });
            document.Dispose();
            await reader;
        }
    }

    [Test]
    public async Task DisposingDocumentClosesOpenPagesAndForms()
    {
        var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        var form = document.LoadForm();
        var page = document.LoadPage(0);
        document.Dispose();

        await Assert.That(() => page.GetText()).Throws<ObjectDisposedException>();
        await Assert.That(() => form.SetHighlight(new(255, 255, 0, 100))).Throws<ObjectDisposedException>();
        // Already closed by the document, so these must not reach PDFium again.
        page.Dispose();
        form.Dispose();
    }

    static (int width, int height) ReadPngSize(byte[] png)
    {
        // IHDR data starts at offset 16: 8 byte signature + 4 byte length + 4 byte type
        var width = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16));
        var height = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20));
        return (width, height);
    }
}
