# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Morph.PDFium is a thin .NET wrapper over the [PDFium](https://pdfium.googlesource.com/pdfium/) C API that renders PDF pages to PNG images and reads document metadata. Native binaries come from the [bblanchon.PDFium.*](https://github.com/bblanchon/pdfium-binaries) NuGet packages (Windows/Linux/macOS). The public API is `PdfRender.PdfiumDocument` (plus `PageSize` and `PdfiumException`); [Verify.PDFium](https://github.com/VerifyTests/Verify.PDFium) consumes this package.

## Build & Test Commands

Tests use **TUnit**, not VSTest. `dotnet test` is unsupported on .NET 10 SDK and will error. Use `dotnet run` against the test project, and TUnit's `--treenode-filter` (not `--filter`) for narrowing:

```bash
# Build
dotnet build src --configuration Release

# All tests
dotnet run --project src/Morph.PDFium.Tests --configuration Release

# Single class
dotnet run --project src/Morph.PDFium.Tests --configuration Release -- --treenode-filter "/*/*/PdfiumDocumentTests/*"

# Single test
dotnet run --project src/Morph.PDFium.Tests --configuration Release -- --treenode-filter "/*/*/PdfiumDocumentTests/PageCount"
```

## Architecture

All source lives under `src/`. Solution file is `src/Morph.PDFium.slnx`.

- **PdfiumNative.cs** — `[LibraryImport]` bindings for the handful of PDFium entry points used. Owns the process-wide `Sync` lock (PDFium is not thread safe; every native call must hold it) and one-time `FPDF_InitLibrary` via the static constructor. The library is never destroyed. Note the `FPDF_GetMetaText` length parameter is natively `unsigned long` (32 bit on Windows, 64 bit elsewhere); `uint` is correct for both.
- **PdfiumDocument.cs** — public API. Loading pins the source byte array for the document lifetime (PDFium reads from it on demand). Rendering: `FPDFBitmap_CreateEx` over a pinned managed buffer, white `FillRect`, `FPDF_RenderPageBitmap` with `FPDF_ANNOT | FPDF_REVERSE_BYTE_ORDER` (RGBA output), then PNG-encode outside the lock. `GetProperties` reads the document information dictionary via the call-twice `FPDF_GetMetaText` length negotiation.
- **PngEncoder.cs** — dependency-free PNG writer: RGBA, Up filter, `ZLibStream` (SmallestSize), `pHYs` chunk for dpi. Deflate output is technically allowed to change between .NET runtime versions; if a runtime upgrade shifts snapshot bytes, regenerate verified files.

Style note: only public types get a namespace declaration (`PdfRender`); internal types live in the global namespace.

## Testing

- Snapshot tests use Verify.TUnit with SSIM comparison for PNGs (`VerifierSettings.UseSsimForPng()`); verified PNG/txt files are committed beside the tests.
- Test assets `sample.pdf` (1 page) and `multi-page.pdf` (4 pages), both US Letter, were produced by [Morph](https://github.com/Papyrine/Morph)'s PDF exporter with embedded font subsets, so rendering is machine-independent.
- PDFium rasterization is deterministic for a pinned bblanchon.PDFium version. Bumping those packages may shift pixels — expect to regenerate `*.verified.png` files in the same commit. The three `bblanchon.PDFium.*` packages must stay on the same version.

## Package Management

Central Package Management; versions live in `src/Directory.Packages.props`.
