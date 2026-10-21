// PNG encoding of every page, rasterised once up front so PDFium is out of the measurement.
[MemoryDiagnoser]
[SimpleJob(warmupCount: 2, iterationCount: 8)]
public class EncodeBenchmarks
{
    (byte[] Pixels, int Width, int Height)[] pages = [];

    [Params(96, 200)]
    public double Dpi { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        using var document = PdfiumDocument.Load(BenchmarkPdf.Path);
        pages = new (byte[], int, int)[document.PageCount];
        for (var index = 0; index < pages.Length; index++)
        {
            pages[index] = document.RenderPixels(index, Dpi, PdfiumNative.RenderAnnotations, PdfiumDocument.OpaqueWhite, null);
        }
    }

    [Benchmark]
    public long Encode()
    {
        long total = 0;
        foreach (var (pixels, width, height) in pages)
        {
            total += PngEncoder.Encode(pixels, width, height, Dpi).Length;
        }

        return total;
    }
}
