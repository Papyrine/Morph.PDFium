# Contributing

## Build and test

Requires the .NET SDK pinned in `global.json`.

```bash
dotnet build src --configuration Release
dotnet run --project src/Morph.PDFium.Tests --configuration Release
```

Tests use TUnit (`dotnet run`, not `dotnet test`) and Verify snapshots. When a deliberate rendering change shifts output, accept the `*.received.*` files as `*.verified.*` and commit them with the change.

## Design constraints

- **PDFium is not thread safe.** Every native call must hold `PdfiumNative.Sync`. Do not add an API that calls PDFium outside that lock.
- **The source buffer must stay pinned.** `FPDF_LoadMemDocument` does not copy the input; `PdfiumDocument` owns a pinned `GCHandle` until `Dispose`.
- **No image library dependencies.** PNG encoding is hand-rolled in `PngEncoder` on purpose; do not add SkiaSharp/ImageSharp for convenience.
- **Determinism is a feature.** Output PNGs are consumed by snapshot tooling ([Verify.PDFium](https://github.com/VerifyTests/Verify.PDFium)). Anything that makes rendering machine-dependent is a bug.

## Updating PDFium

Bump the three `bblanchon.PDFium.*` packages in `src/Directory.Packages.props` together, run the test suite, and commit any snapshot changes produced by the new rasterizer in the same PR. Downstream, Verify.PDFium pins a Morph.PDFium version, so publish Morph.PDFium first and bump it there.
