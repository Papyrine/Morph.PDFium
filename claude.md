# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Morph.PDFium is a .NET wrapper over the [PDFium](https://pdfium.googlesource.com/pdfium/) C API. It started as a render-to-PNG + metadata wrapper and now covers a broad slice of the PDFium surface: rendering (DPI, clip/region, grayscale, render flags), text extraction & search, navigation (bookmarks/destinations/actions/links), annotations, AcroForm fields, page manipulation (import/rotate/insert/delete/flatten), content editing, save, attachments, signatures, tagged-structure and thumbnails. Native binaries come from the [bblanchon.PDFium.*](https://github.com/bblanchon/pdfium-binaries) NuGet packages (Windows/Linux/macOS). The public API lives in the `Morph.PDFium` namespace — entry point `PdfiumDocument`, with `PdfPage` and `PdfForm` for page- and form-scoped work; [Verify.PDFium](https://github.com/VerifyTests/Verify.PDFium) consumes this package. See [docs/native-api-coverage.md](docs/native-api-coverage.md) for exactly which of the 460 native exports are wrapped and which are intentionally not.

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

The native bindings and the public API are each split into per-feature partial files:

- **PdfiumNative.cs** + **PdfiumNative.\*.cs** (`.Document`, `.Text`, `.Doc`, `.Edit`, `.More`, `.Render`, `.Form`, `.Objects`) — `[LibraryImport]` bindings grouped by source header. `PdfiumNative.cs` owns the process-wide `Sync` lock (PDFium is not thread safe; every native call must hold it) and one-time `FPDF_InitLibrary` via the static constructor. The library is never destroyed. PDFium's `unsigned long` length parameters are 32 bit on Windows / 64 bit elsewhere; `uint` is correct for both.
- **Interop.cs** — shared marshalling helpers for PDFium's "call twice" string protocol (`Utf16ByLength`/`Utf8ByLength`/`Utf16ByUnits`, `ToWideString`) plus the blittable structs `FsRectF`/`FsMatrix`/`FsQuadPoints`. All assume the caller holds `Sync`.
- **PdfiumDocument.cs** + **PdfiumDocument.\*.cs** — public document API: load/render core plus `.Info`, `.Pages`, `.Bookmarks`, `.Edit`, `.Save`, `.Attachments`, `.Signatures`, `.Render`, `.Forms`. Loading pins the source bytes for the document lifetime; `CreateNew` makes an empty document with no pinned buffer. The shared rasteriser `RenderPixels(index, dpi, flags, region, formHandle)` renders into a pinned managed buffer with `FPDF_REVERSE_BYTE_ORDER` (RGBA), then PNG-encodes outside the lock; pass a `ClipRegion` for `FPDF_RenderPageBitmapWithMatrix`, or a form handle to overlay widgets via `FPDF_FFLDraw`.
- **PdfPage.cs** + **PdfPage.\*.cs** (`.Text`, `.Links`, `.Annotations`, `.Edit`, `.Objects`) — a disposable page handle wrapping `FPDF_LoadPage`. Text and web-link sub-handles are loaded lazily and closed on dispose.
- **PdfForm.cs** — disposable `FPDF_FORMHANDLE` session. The `FPDF_FORMFILLINFO` struct (one `version` int + 32 callback slots, all null for headless use) must stay pinned for the handle's lifetime, since PDFium retains the pointer.
- **Save** uses a `FPDF_FILEWRITE` whose `WriteBlock` is an `[UnmanagedCallersOnly]` cdecl function pointer; the destination `Stream` is recovered via a GCHandle stored in a trailing struct slot.
- **PngEncoder.cs** — dependency-free PNG writer: RGBA, Up filter, `ZLibStream` (SmallestSize), `pHYs` chunk for dpi. Deflate output is technically allowed to change between .NET runtime versions; if a runtime upgrade shifts snapshot bytes, regenerate verified files.

Style note: only public types get a namespace declaration (`Morph.PDFium`); internal types (`PdfiumNative`, `Interop`, the `Fs*` structs, `Navigation`) live in the global namespace.

Build note: the `Release` pack step runs `SponsorCheck`, which needs a GitHub token. To compile-check without packaging, build the project in `Debug` (`dotnet build src/Morph.PDFium/Morph.PDFium.csproj -c Debug`); the SponsorCheck target only fires for `Release` + packable.

## Testing

- Snapshot tests use Verify.TUnit with SSIM comparison for PNGs (`VerifierSettings.UseSsimForPng()`); verified PNG/txt files are committed beside the tests.
- Test assets `sample.pdf` (1 page) and `multi-page.pdf` (4 pages), both US Letter, were produced by [Morph](https://github.com/Papyrine/Morph)'s PDF exporter with embedded font subsets, so rendering is machine-independent.
- PDFium rasterization is deterministic for a pinned bblanchon.PDFium version. Bumping those packages may shift pixels — expect to regenerate `*.verified.png` files in the same commit. The three `bblanchon.PDFium.*` packages must stay on the same version.

## Package Management

Central Package Management; versions live in `src/Directory.Packages.props`.
