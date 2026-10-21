// The document the benchmarks render. Set MORPH_PDFIUM_BENCHMARK_PDF to measure a real-world
// file; BenchmarkDotNet runs each benchmark in a child process, which inherits the variable.
static class BenchmarkPdf
{
    public static string Path { get; } =
        Environment.GetEnvironmentVariable("MORPH_PDFIUM_BENCHMARK_PDF") ?? DefaultPath();

    // Resolved from this source file's location at compile time, since the benchmarks run
    // from BenchmarkDotNet's generated output directory rather than this project's.
    static string DefaultPath([CallerFilePath] string source = "") =>
        System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(source)!, "..", "Morph.PDFium.Tests", "multi-page.pdf"));
}
