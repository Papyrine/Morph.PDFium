// End to end conversion of every page to PNG, alongside the PDFium rasterisation on its own.
// RenderPageLoop less RenderPixelsOnly is the cost of PNG encoding, and RenderPages against
// RenderPageLoop shows what overlapping the encoding with rendering gains.
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

    // One page at a time, as a caller converting pages individually (such as Verify.PDFium) does.
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
            var (pixels, _, _) = document.RenderPixels(index, Dpi, PdfiumNative.RenderAnnotations, PdfiumDocument.OpaqueWhite, null);
            total += pixels.Length;
        }

        return total;
    }
}
