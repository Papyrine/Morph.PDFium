// Reads that go through PDFium's "call twice" string protocol or decode data, all under the
// PDFium lock, and the head of a long document's HTML.
[MemoryDiagnoser]
[SimpleJob(warmupCount: 2, iterationCount: 8)]
public class ReadBenchmarks
{
    PdfiumDocument form = null!;
    PdfForm formSession = null!;
    PdfPage formPage = null!;
    PdfiumDocument tagged = null!;
    PdfPage taggedPage = null!;
    PdfiumDocument withAttachment = null!;
    PdfAttachment attachment = null!;
    PdfiumDocument longDocument = null!;
    HtmlOptions firstPage = null!;

    [GlobalSetup]
    public void Setup()
    {
        form = PdfiumDocument.Load(BenchmarkPdf.Asset("irs-f1040.pdf"));
        formSession = form.LoadForm();
        formPage = form.LoadPage(0);
        tagged = PdfiumDocument.Load(BenchmarkPdf.Asset("nasa-training-for-space.pdf"));
        taggedPage = tagged.LoadPage(0);

        // A 16 MB attachment of compressible data, saved so it is stored deflated.
        var data = new byte[16 * 1024 * 1024];
        new Random(1).NextBytes(data.AsSpan(0, data.Length / 2));
        using (var document = PdfiumDocument.Load(BenchmarkPdf.Asset("sample.pdf")))
        {
            document.AddAttachment("data.bin", data);
            withAttachment = PdfiumDocument.Load(document.Save());
        }

        attachment = withAttachment.GetAttachments()[0];

        // 2000 pages, of which the HTML holds only the first.
        longDocument = PdfiumDocument.CreateNew();
        using (var source = PdfiumDocument.Load(BenchmarkPdf.Asset("multi-page.pdf")))
        {
            for (var copy = 0; copy < 500; copy++)
            {
                longDocument.ImportPages(source);
            }
        }

        firstPage = new()
        {
            MaxPages = 1,
            IncludeImage = false
        };
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        formPage.Dispose();
        formSession.Dispose();
        form.Dispose();
        taggedPage.Dispose();
        tagged.Dispose();
        withAttachment.Dispose();
        longDocument.Dispose();
    }

    [Benchmark]
    public int FormFields() =>
        formSession.GetFields(formPage).Count;

    [Benchmark]
    public int StructureTree() =>
        taggedPage.GetStructureTree().Count;

    [Benchmark]
    public int AttachmentData() =>
        attachment.GetData().Length;

    [Benchmark]
    public void FirstPageOfLongDocument() =>
        longDocument.RenderHtml(TextWriter.Null, firstPage);
}
