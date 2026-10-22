// End to end conversion of every page to PNG, alongside the PDFium rasterisation on its own.
// RenderPageLoop less RenderPixelsOnly is the cost of PNG encoding, and RenderPages against
// RenderPageLoop shows what overlapping the encoding with rendering gains. The TextThen pair
// is the shape of a page by page conversion that extracts text as well (as Verify.PDFium
// does), rendering through the document or through the page it already has open.
[MemoryDiagnoser]
[SimpleJob(warmupCount: 2, iterationCount: 8)]
public class RenderBenchmarks
{
    PdfiumDocument document = null!;

    [Params(96, 200)]
    public double Dpi { get; set; }

    [GlobalSetup]
    public void Setup() =>
        document = PdfiumDocument.Load(BenchmarkPdf.Path);

    [GlobalCleanup]
    public void Cleanup() =>
        document.Dispose();

    [Benchmark]
    public List<byte[]> RenderPages() =>
        document.RenderPages(Dpi);

    // One page at a time, as a caller converting pages individually does.
    [Benchmark]
    public long RenderPageLoop()
    {
        long total = 0;
        for (var index = 0; index < document.PageCount; index++)
        {
            total += document.RenderPage(index, Dpi).Length;
        }

        return total;
    }

    [Benchmark]
    public long RenderPixelsOnly()
    {
        long total = 0;
        for (var index = 0; index < document.PageCount; index++)
        {
            using var rendered = document.RenderPixels(index, Dpi, PdfiumNative.RenderAnnotations, PdfiumDocument.OpaqueWhite, null);
            total += rendered.Pixels.Length;
        }

        return total;
    }

    [Benchmark]
    public long TextThenRenderPage()
    {
        long total = 0;
        for (var index = 0; index < document.PageCount; index++)
        {
            using var page = document.LoadPage(index);
            total += page.GetText()?.Length ?? 0;
            total += document.RenderPage(index, Dpi).Length;
        }

        return total;
    }

    [Benchmark]
    public long TextThenRenderOpenPage()
    {
        long total = 0;
        for (var index = 0; index < document.PageCount; index++)
        {
            using var page = document.LoadPage(index);
            total += page.GetText()?.Length ?? 0;
            total += page.Render(Dpi).Length;
        }

        return total;
    }
}
