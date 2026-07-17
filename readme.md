# <img src="/src/icon.png" height="30px"> Morph.PDFium

[![Build status](https://img.shields.io/appveyor/build/SimonCropp/Morph-PDFium)](https://ci.appveyor.com/project/SimonCropp/Morph-PDFium)
[![NuGet Status](https://img.shields.io/nuget/v/Morph.PDFium.svg?label=Morph.PDFium)](https://www.nuget.org/packages/Morph.PDFium/)

A .NET wrapper over [PDFium](https://pdfium.googlesource.com/pdfium/), using the prebuilt native binaries from [pdfium-binaries](https://github.com/bblanchon/pdfium-binaries) (Windows, Linux, and macOS). No image library dependency: PNG encoding is built in.

It renders pages to PNG and also covers text extraction and search, navigation (bookmarks, destinations, links), annotations, AcroForm fields, page manipulation (import/merge, rotate, insert, delete, flatten), content editing, save, attachments and signatures. See [native API coverage](docs/native-api-coverage.md) for the full wrapped surface.

**See [Milestones](../../milestones?state=closed) for release notes.**


## Open Source Maintenance Fee

This project participates in the [Open Source Maintenance Fee](https://opensourcemaintenancefee.org). The source code is freely available under the terms of the [license](license.txt). To support sustainable maintenance, use of the project's official binary releases in revenue-generating activities and all government agencies requires adherence to the [Open Source Maintenance Fee EULA](OsmfEula.txt). The fee is paid by [sponsoring Papyrine](https://github.com/sponsors/Papyrine).

This project uses [SponsorCheck](https://github.com/SimonCropp/SponsorCheck) to surface a build-time reminder in consuming projects that are not yet sponsoring.


## NuGet package

[Morph.PDFium](https://www.nuget.org/packages/Morph.PDFium/)


## Native binaries

Morph.PDFium references all three [pdfium-binaries](https://github.com/bblanchon/pdfium-binaries) packages (`bblanchon.PDFium.Win32`, `bblanchon.PDFium.Linux` and `bblanchon.PDFium.macOS`), so it runs on Windows, Linux and macOS with no extra setup. The trade-off is that a restore pulls all three (~50 MB), and a runtime-agnostic build copies every runtime into `bin` (~110 MB across 12 RIDs).

None of that reaches a published app. NuGet copies only the binary matching the target runtime, so `dotnet publish -r win-x64` emits a single `pdfium.dll`.

To trim the build output as well, set [`UseCurrentRuntimeIdentifier`](https://learn.microsoft.com/dotnet/core/compatibility/sdk/7.0/automatic-runtimeidentifier) in an app or test project:

```xml
<PropertyGroup>
  <UseCurrentRuntimeIdentifier>true</UseCurrentRuntimeIdentifier>
  <AppendRuntimeIdentifierToOutputPath>false</AppendRuntimeIdentifierToOutputPath>
</PropertyGroup>
```

`UseCurrentRuntimeIdentifier` resolves to the runtime of the machine doing the build (`win-x64` locally, `linux-x64` on CI, and so on), so only the matching `pdfium.dll` is copied. `AppendRuntimeIdentifierToOutputPath` keeps output at `bin/<configuration>/<tfm>/`, which the runtime identifier would otherwise push down to `bin/<configuration>/<tfm>/<rid>/`.

Both affect build output only: a packed library is keyed by target framework rather than runtime, so setting them in one changes nothing about the resulting package.


## Usage


### Render a page

<!-- snippet: RenderPage -->
<a id='snippet-RenderPage'></a>
```cs
using var document = PdfiumDocument.Load("sample.pdf");
var png = document.RenderPage(0, dpi: 96);
```
<sup><a href='/src/Morph.PDFium.Tests/Samples.cs#L8-L13' title='Snippet source file'>snippet source</a> | <a href='#snippet-RenderPage' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Render all pages

<!-- snippet: RenderPages -->
<a id='snippet-RenderPages'></a>
```cs
using var document = PdfiumDocument.Load("multi-page.pdf");
List<byte[]> pages = document.RenderPages();
```
<sup><a href='/src/Morph.PDFium.Tests/Samples.cs#L23-L28' title='Snippet source file'>snippet source</a> | <a href='#snippet-RenderPages' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Document metadata

<!-- snippet: DocumentInfo -->
<a id='snippet-DocumentInfo'></a>
```cs
using var document = PdfiumDocument.Load("multi-page.pdf");
Console.WriteLine(document.PageCount);
Console.WriteLine(document.GetPageSizes());
Console.WriteLine(document.GetProperties());
```
<sup><a href='/src/Morph.PDFium.Tests/Samples.cs#L40-L47' title='Snippet source file'>snippet source</a> | <a href='#snippet-DocumentInfo' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Grayscale and other render options

`RenderOptions` controls dpi, grayscale, anti-aliasing, print optimization and background.

<!-- snippet: RenderGrayscale -->
<a id='snippet-RenderGrayscale'></a>
```cs
using var document = PdfiumDocument.Load("sample.pdf");
var png = document.RenderPage(0, new RenderOptions
{
    Grayscale = true,
    Dpi = 150
});
```
<sup><a href='/src/Morph.PDFium.Tests/Samples.cs#L144-L153' title='Snippet source file'>snippet source</a> | <a href='#snippet-RenderGrayscale' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Render a region (clip / tile)

Render a single rectangle of a page (in page points, origin bottom-left), scaled to the dpi.

<!-- snippet: RenderRegion -->
<a id='snippet-RenderRegion'></a>
```cs
using var document = PdfiumDocument.Load("sample.pdf");
// A clip rectangle in page points (origin bottom-left): the top-left quadrant.
var clip = new PdfRectangle(0, 396, 306, 792);
var png = document.RenderRegion(
    0,
    clip,
    new()
    {
        Dpi = 96
    });
```
<sup><a href='/src/Morph.PDFium.Tests/Samples.cs#L161-L174' title='Snippet source file'>snippet source</a> | <a href='#snippet-RenderRegion' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Extract text

<!-- snippet: ExtractText -->
<a id='snippet-ExtractText'></a>
```cs
using var document = PdfiumDocument.Load("sample.pdf");
using var page = document.LoadPage(0);
var text = page.GetText();
```
<sup><a href='/src/Morph.PDFium.Tests/Samples.cs#L61-L67' title='Snippet source file'>snippet source</a> | <a href='#snippet-ExtractText' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Search within a page

<!-- snippet: SearchText -->
<a id='snippet-SearchText'></a>
```cs
using var document = PdfiumDocument.Load("sample.pdf");
using var page = document.LoadPage(0);
var matches = page.Search("paragraph");
// Map a match back to rectangles on the page (e.g. for highlighting).
var first = matches[0];
var rectangles = page.GetTextRectangles(first.CharIndex, first.CharCount);
```
<sup><a href='/src/Morph.PDFium.Tests/Samples.cs#L75-L84' title='Snippet source file'>snippet source</a> | <a href='#snippet-SearchText' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Read bookmarks (outline)

<!-- snippet: Bookmarks -->
<a id='snippet-Bookmarks'></a>
```cs
using var document = PdfiumDocument.Load("multi-page.pdf");
foreach (var bookmark in document.GetBookmarks())
{
    Console.WriteLine($"{bookmark.Title} -> page {bookmark.Destination?.PageIndex}");
}
```
<sup><a href='/src/Morph.PDFium.Tests/Samples.cs#L93-L101' title='Snippet source file'>snippet source</a> | <a href='#snippet-Bookmarks' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Read annotations

<!-- snippet: Annotations -->
<a id='snippet-Annotations'></a>
```cs
using var document = PdfiumDocument.Load("sample.pdf");
using var page = document.LoadPage(0);
foreach (var annotation in page.GetAnnotations())
{
    Console.WriteLine($"{annotation.Type}: {annotation.Contents}");
}
```
<sup><a href='/src/Morph.PDFium.Tests/Samples.cs#L109-L118' title='Snippet source file'>snippet source</a> | <a href='#snippet-Annotations' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Read AcroForm fields

<!-- snippet: FormFields -->
<a id='snippet-FormFields'></a>
```cs
using var document = PdfiumDocument.Load("sample.pdf");
using var form = document.LoadForm();
using var page = document.LoadPage(0);
foreach (var field in form.GetFields(page))
{
    Console.WriteLine($"{field.Name} ({field.Type}) = {field.Value}");
}
```
<sup><a href='/src/Morph.PDFium.Tests/Samples.cs#L126-L136' title='Snippet source file'>snippet source</a> | <a href='#snippet-FormFields' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Merge / import pages

<!-- snippet: MergeDocuments -->
<a id='snippet-MergeDocuments'></a>
```cs
using var merged = PdfiumDocument.CreateNew();
using (var first = PdfiumDocument.Load("sample.pdf"))
using (var second = PdfiumDocument.Load("multi-page.pdf"))
{
    merged.ImportPages(first);
    merged.ImportPages(second, "1-2");
}

var bytes = merged.Save();
```
<sup><a href='/src/Morph.PDFium.Tests/Samples.cs#L182-L194' title='Snippet source file'>snippet source</a> | <a href='#snippet-MergeDocuments' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Edit and save

Add content (text, rectangles), then serialize with `Save`.

<!-- snippet: EditAndSave -->
<a id='snippet-EditAndSave'></a>
```cs
using var document = PdfiumDocument.Load("sample.pdf");
using (var page = document.LoadPage(0))
{
    page.AddRectangle(new(40, 700, 240, 760), fill: new(220, 230, 250, 255));
    page.AddText("Stamped", 50, 720, fontSize: 24);
}

var stamped = document.Save();
```
<sup><a href='/src/Morph.PDFium.Tests/Samples.cs#L203-L214' title='Snippet source file'>snippet source</a> | <a href='#snippet-EditAndSave' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Embedded file attachments

<!-- snippet: Attachments -->
<a id='snippet-Attachments'></a>
```cs
using var document = PdfiumDocument.Load("sample.pdf");
document.AddAttachment("notes.txt", [.. "embedded data"u8]);
var withAttachment = document.Save();
```
<sup><a href='/src/Morph.PDFium.Tests/Samples.cs#L222-L228' title='Snippet source file'>snippet source</a> | <a href='#snippet-Attachments' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

For the full list of native PDFium entry points that are wrapped (and those intentionally left out), see [native API coverage](docs/native-api-coverage.md).


## Behavior notes

 * Pages render with a white background and annotations included.
 * Output dimensions are `pageSizeInPoints / 72 * dpi`, rounded to the nearest pixel. The default 96 dpi renders an A4 page at 794 x 1123 and a Letter page at 816 x 1056.
 * The PNG includes a `pHYs` chunk recording the render dpi.
 * PDFium is not thread safe, so all native calls are serialized on a process-wide lock. `PdfiumDocument` instances can safely be used from multiple threads, but rendering does not parallelize.
 * Rendering is deterministic for a given Morph.PDFium version: the same input produces byte-identical PNGs on every machine and OS, which makes the output suitable for snapshot testing. [Verify.PDFium](https://github.com/VerifyTests/Verify.PDFium) builds on this.


## Icon

[PDF](https://thenounproject.com/icon/pdf-7564953//) designed by [Meilia](https://thenounproject.com/creator/meilia1/) from [The Noun Project](https://thenounproject.com).
