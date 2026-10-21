// Runs from the repository root with, for example:
//   dotnet run --project src/Morph.PDFium.Benchmarks --configuration Release -- --filter *
// The benchmarks render src/Morph.PDFium.Tests/multi-page.pdf unless MORPH_PDFIUM_BENCHMARK_PDF names another file.
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
