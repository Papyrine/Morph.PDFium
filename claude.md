# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Morph.PDFium is a .NET wrapper over the [PDFium](https://pdfium.googlesource.com/pdfium/) C API. It started as a render-to-PNG + metadata wrapper and now covers a broad slice of the PDFium surface: rendering (DPI, clip/region, grayscale, render flags), HTML rendering (page image with selectable SVG text overlay, links, outline), text extraction & search, navigation (bookmarks/destinations/actions/links), annotations, AcroForm fields, page manipulation (import/rotate/insert/delete/flatten), content editing, save, attachments, signatures, tagged-structure and thumbnails. Native binaries come from the [bblanchon.PDFium.*](https://github.com/bblanchon/pdfium-binaries) NuGet packages (Windows/Linux/macOS). The public API lives in the `Morph.PDFium` namespace — entry point `PdfiumDocument`, with `PdfPage` and `PdfForm` for page- and form-scoped work; [Verify.PDFium](https://github.com/VerifyTests/Verify.PDFium) consumes this package. See [docs/native-api-coverage.md](docs/native-api-coverage.md) for exactly which of the 460 native exports are wrapped and which are intentionally not.

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

# Benchmarks (BenchmarkDotNet); renders multi-page.pdf unless MORPH_PDFIUM_BENCHMARK_PDF names another file
dotnet run --project src/Morph.PDFium.Benchmarks --configuration Release -- --filter "*"
```

`Morph.PDFium.Benchmarks` and `Morph.PDFium.Tests` have `InternalsVisibleTo` (with the key.snk public key) so they can reach `RenderPixels` and `PngEncoder` apart from the public API.

## Architecture

All source lives under `src/`. Solution file is `src/Morph.PDFium.slnx`.

The native bindings and the public API are each split into per-feature partial files:

- **PdfiumNative.cs** + **PdfiumNative.\*.cs** (`.Document`, `.Text`, `.Doc`, `.Edit`, `.More`, `.Render`, `.Form`, `.Objects`) — `[LibraryImport]` bindings grouped by source header. `PdfiumNative.cs` owns the process-wide `Sync` lock (PDFium is not thread safe; every native call must hold it) and one-time `FPDF_InitLibrary` via the static constructor. The library is never destroyed. PDFium's `unsigned long` length parameters are 32 bit on Windows / 64 bit elsewhere; `uint` is correct for both.
- **Interop.cs** — shared marshalling helpers for PDFium's "call twice" string protocol (`Utf16ByLength`/`Utf8ByLength`/`Utf16ByUnits`, `ToWideString`) plus the blittable structs `FsRectF`/`FsMatrix`/`FsQuadPoints`. All assume the caller holds `Sync`.
- **PdfiumDocument.cs** + **PdfiumDocument.\*.cs** — public document API: load/render core plus `.Info`, `.Pages`, `.Bookmarks`, `.Edit`, `.Save`, `.Attachments`, `.Signatures`, `.Render`, `.Forms`. Loading pins the source bytes for the document lifetime; `CreateNew` makes an empty document with no pinned buffer. The shared rasteriser `RenderPixels(index, dpi, flags, region, formHandle)` renders into a pinned buffer rented from `ArrayPool` with `FPDF_REVERSE_BYTE_ORDER` (RGBA), handed back as a disposable `RenderedPixels`, then PNG-encodes outside the lock; pass a `ClipRegion` for `FPDF_RenderPageBitmapWithMatrix`, or a form handle to overlay widgets via `FPDF_FFLDraw`.
- **PdfPage.cs** + **PdfPage.\*.cs** (`.Text`, `.Links`, `.Annotations`, `.Edit`, `.Objects`) — a disposable page handle wrapping `FPDF_LoadPage`. Text and web-link sub-handles are loaded lazily and closed on dispose. `Rendering/PdfPage.cs` adds `Render`/`RenderRegion` on an open page, which reuse the loaded handle via `RenderLoadedPage` rather than loading (and so parsing) the page a second time.
- **Html/** — HTML rendering. `PdfPage.RenderHtml` reads text runs and links under `Sync` (`ReadHtmlLayout`), renders the PNG via `Render`, then `HtmlWriter` builds a fragment outside the lock: a `div` with inline styles holding the `<img>` and an inline SVG whose `viewBox` is the displayed page in points. Text runs are `<text textLength lengthAdjust="spacingAndGlyphs">` so the browser's font is stretched to the PDF's width; `HtmlTextRunBuilder` groups chars into runs (same baseline, size, angle, style; breaks on gaps/jumps). Page→display coords come from a `DisplayMatrix` derived via three `FPDF_PageToDevice` calls (handles `/Rotate` and offset crop boxes). Note `FPDFText_GetCharAngle` is clockwise. `PdfiumDocument.RenderHtml` wraps pages (via `ProcessPages`) with head metadata, `lang`, the outline and print CSS. Link hrefs go through `HtmlWriter.SafeUrl` (scheme allowlist).
- **PdfForm.cs** — disposable `FPDF_FORMHANDLE` session. The `FPDF_FORMFILLINFO` struct (one `version` int + 32 callback slots, all null for headless use) must stay pinned for the handle's lifetime, since PDFium retains the pointer.
- **Save** uses a `FPDF_FILEWRITE` whose `WriteBlock` is an `[UnmanagedCallersOnly]` cdecl function pointer; the destination `Stream` is recovered via a GCHandle stored in a trailing struct slot.
- **PngEncoder.cs** — dependency-free PNG writer: 8-bit grayscale when every pixel is opaque gray (the common text-on-white page; a quarter of the data to deflate) — one fused `Vector256`/`Vector128` pass both checks that and extracts the gray plane — otherwise RGBA; None or Up filter, chosen per image by deflating a 64-row sample both ways at `Fastest` (None wins on text, whose rows repeat; Up on photos and gradients); `ZLibStream` at `RenderOptions.CompressionLevel` (a `PngCompressionLevel`, zlib 0–9, per render, or process-wide via `RenderOptions.DefaultCompressionLevel`; default `Optimal` = 6); output grows through pooled buffers; `pHYs` chunk for dpi; chunk CRCs by slicing-by-eight tables (the x86 CRC instruction uses the wrong polynomial, and ARM's is untested by CI). Encoding, not PDFium, dominates conversion time, which is also why `ProcessPages` (and `RenderPages`, built on it) runs pages in parallel: PDFium work is serialized on `Sync` but each page's managed work — PNG encoding, text handling — overlaps it. Deflate output is technically allowed to change between .NET runtime versions; if a runtime upgrade shifts snapshot bytes, regenerate verified files.

Style note: only public types get a namespace declaration (`Morph.PDFium`); internal types (`PdfiumNative`, `Interop`, the `Fs*` structs, `Navigation`) live in the global namespace.

## Testing

- Snapshot tests use Verify.TUnit with SSIM comparison for PNGs (`VerifierSettings.UseSsimForPng()`); verified PNG/txt files are committed beside the tests.
- Test assets `sample.pdf` (1 page) and `multi-page.pdf` (4 pages), both US Letter, were produced by [Morph](https://github.com/Papyrine/Morph)'s PDF exporter with embedded font subsets, so rendering is machine-independent.
- PDFium rasterization is deterministic for a pinned bblanchon.PDFium version. Bumping those packages may shift pixels — expect to regenerate `*.verified.png` files in the same commit. The three `bblanchon.PDFium.*` packages must stay on the same version.

## Package Management

Central Package Management; versions live in `src/Directory.Packages.props`.

## Audits

- 2026-10-11: last full performance and bug scan of the library.
