// Conversion of the whole document to HTML, with each page's image embedded, written to a
// writer that discards it, so the cost is the conversion and not where the HTML goes.
[MemoryDiagnoser]
[SimpleJob(warmupCount: 2, iterationCount: 8)]
public class HtmlBenchmarks
{
    PdfiumDocument document = null!;
    HtmlOptions options = null!;

    [Params(96, 200)]
    public double Dpi { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        document = PdfiumDocument.Load(BenchmarkPdf.Path);
        options = new()
        {
            Render = new()
            {
                Dpi = Dpi
            }
        };
    }

    [GlobalCleanup]
    public void Cleanup() =>
        document.Dispose();

    [Benchmark]
    public void RenderHtml() =>
        document.RenderHtml(TextWriter.Null, options);
}
