# <img src="/src/icon.png" height="30px"> Morph.PDFium

[![Build status](https://github.com/Papyrine/Morph.PDFium/actions/workflows/build.yml/badge.svg)](https://github.com/Papyrine/Morph.PDFium/actions/workflows/build.yml)
[![NuGet Status](https://img.shields.io/nuget/v/Morph.PDFium.svg?label=Morph.PDFium)](https://www.nuget.org/packages/Morph.PDFium/)

A .NET wrapper over [PDFium](https://pdfium.googlesource.com/pdfium/), using the prebuilt native binaries from [pdfium-binaries](https://github.com/bblanchon/pdfium-binaries) (Windows, Linux, and macOS). No image library dependency: PNG encoding is built in.

It renders pages to PNG or HTML and also covers text extraction and search, navigation (bookmarks, destinations, links), annotations, AcroForm fields, page manipulation (import/merge, rotate, insert, delete, flatten), content editing, save, attachments and signatures. See [native API coverage](docs/native-api-coverage.md) for the full wrapped surface.

**See [Milestones](../../milestones?state=closed) for release notes.**


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


### Convert every page in parallel

`ProcessPages` loads each page, hands it to a callback and collects the results in page order, with the pages spread across cores. PDFium works on one page at a time, but the PNG encoding and whatever else the callback does for a page overlap the native work on the others, which makes converting a whole document this way 1.7–2.3× faster than a page-by-page loop. The callback runs on several threads at once and must not keep the page, which is disposed when it returns. `RenderPages` is built on it.

<!-- snippet: ProcessPages -->
<a id='snippet-ProcessPages'></a>
```cs
using var document = PdfiumDocument.Load("multi-page.pdf");
// Pages are spread across cores: PDFium works on one at a time, the PNG encoding overlaps
var pages = document.ProcessPages(_ => (Text: _.GetText(), Png: _.Render(dpi: 96)));
```
<sup><a href='/src/Morph.PDFium.Tests/Samples.cs#L201-L207' title='Snippet source file'>snippet source</a> | <a href='#snippet-ProcessPages' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Document metadata

<!-- snippet: DocumentInfo -->
<a id='snippet-DocumentInfo'></a>
```cs
using var document = PdfiumDocument.Load("multi-page.pdf");
Console.WriteLine(document.PageCount);
foreach (var size in document.GetPageSizes())
{
    Console.WriteLine($"{size.Width} x {size.Height}");
}

foreach (var (name, value) in document.GetProperties() ?? [])
{
    Console.WriteLine($"{name}: {value}");
}
```
<sup><a href='/src/Morph.PDFium.Tests/Samples.cs#L40-L54' title='Snippet source file'>snippet source</a> | <a href='#snippet-DocumentInfo' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Grayscale and other render options

`RenderOptions` controls dpi, grayscale, anti-aliasing, print optimization and background.

<!-- snippet: RenderGrayscale -->
<a id='snippet-RenderGrayscale'></a>
```cs
using var document = PdfiumDocument.Load("sample.pdf");
var png = document.RenderPage(
    0,
    new RenderOptions
    {
        Grayscale = true,
        Dpi = 150
    });
```
<sup><a href='/src/Morph.PDFium.Tests/Samples.cs#L151-L162' title='Snippet source file'>snippet source</a> | <a href='#snippet-RenderGrayscale' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### PNG compression level

Pages are deflated at `PngCompressionLevel.Optimal` (zlib level 6) by default. Lower levels encode faster for a somewhat larger file: on text pages `Level4` is about 20% faster for files about 3% larger. Set it per render, or globally for the renders that take no options.

<!-- snippet: CompressionLevel -->
<a id='snippet-CompressionLevel'></a>
```cs
using var document = PdfiumDocument.Load("sample.pdf");
// For one render; Optimal (zlib level 6) is the default
var png = document.RenderPage(
    0,
    new RenderOptions
    {
        CompressionLevel = PngCompressionLevel.Level4
    });

// For every render not given a level of its own
RenderOptions.DefaultCompressionLevel = PngCompressionLevel.Level4;
```
<sup><a href='/src/Morph.PDFium.Tests/Samples.cs#L174-L188' title='Snippet source file'>snippet source</a> | <a href='#snippet-CompressionLevel' title='Start of snippet'>anchor</a></sup>
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
<sup><a href='/src/Morph.PDFium.Tests/Samples.cs#L340-L353' title='Snippet source file'>snippet source</a> | <a href='#snippet-RenderRegion' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Render an open page

A page already loaded for its text or annotations can be rendered directly, which skips parsing it a second time.

<!-- snippet: RenderOpenPage -->
<a id='snippet-RenderOpenPage'></a>
```cs
using var document = PdfiumDocument.Load("sample.pdf");
using var page = document.LoadPage(0);
var text = page.GetText();
// Renders the page that is already loaded, rather than parsing it again
var png = page.Render(dpi: 96);
```
<sup><a href='/src/Morph.PDFium.Tests/Samples.cs#L215-L223' title='Snippet source file'>snippet source</a> | <a href='#snippet-RenderOpenPage' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Render to HTML

`RenderHtml` converts a document to a single HTML page. Each page is its rendered image with the page's text laid exactly over it, transparent, in an inline SVG, so the HTML looks like the PDF while its text can be selected, copied and found with the browser's search. Links to other pages, URLs and neighbouring files stay clickable, and the outline becomes a list of links at the top. The pages are rendered in parallel, as `ProcessPages` does. Each page prints on a sheet of its own size, with no margins, so a document mixing portrait, landscape and odd sizes prints one sheet per page.

<!-- snippet: RenderHtml -->
<a id='snippet-RenderHtml'></a>
```cs
using var document = PdfiumDocument.Load("multi-page.pdf");
// Each page's image with its text selectable over it; the images are embedded
var html = document.RenderHtml();
```
<sup><a href='/src/Morph.PDFium.Tests/Samples.cs#L232-L238' title='Snippet source file'>snippet source</a> | <a href='#snippet-RenderHtml' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Images are embedded as data URIs by default, which keeps the HTML self-contained. `HtmlOptions.ImageUrl` lets them be written out as files instead, and `HtmlOptions.Render` sets how they are rendered. For another image format, such as WebP or JPEG, set `HtmlOptions.ImageUrlFromPixels` instead: it is handed each page's RGBA pixels before any PNG is encoded, to encode with an image library and return the image's URL (a file beside the HTML, or a data URI). Images after the first page load lazily as they are scrolled near; set `LazyLoadImages = false` for HTML that is printed or captured without scrolling.

For a long document, pass a `TextWriter` (a `StreamWriter` over a file, say) to `RenderHtml`. Each page is written as soon as it and the pages before it are done, so only a few pages are in memory at once. Converting 1,000 pages at 150 DPI this way peaks at about half the memory of building the whole string.

`HtmlOptions.MaxPages` renders only a document's first pages, without changing the document. A bookmark to a page left out stays in the outline as plain text, links to those pages are dropped, and only the pages rendered get a printed page size.

<!-- snippet: RenderHtmlOptions -->
<a id='snippet-RenderHtmlOptions'></a>
```cs
using var document = PdfiumDocument.Load("multi-page.pdf");
var html = document.RenderHtml(
    new HtmlOptions
    {
        Render = new()
        {
            Dpi = 150
        },
        // Write each page's image beside the HTML rather than embedding it
        ImageUrl = (index, png) =>
        {
            var name = $"page{index + 1}.png";
            File.WriteAllBytes(Path.Combine(directory, name), png);
            return name;
        }
    });
File.WriteAllText(Path.Combine(directory, "document.html"), html);
```
<sup><a href='/src/Morph.PDFium.Tests/Samples.cs#L249-L269' title='Snippet source file'>snippet source</a> | <a href='#snippet-RenderHtmlOptions' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

With `HtmlTextMode.Visible`, the HTML draws the text itself. It keeps its color, weight and style, filled or outlined as the PDF draws it, stretched to the width it has on the page. It uses the PDF font's family when the browser has that font, otherwise the nearest generic family; the PDF's embedded fonts are not carried over. The page image is then rendered without its text, so images and drawings stay while the text is sharp at any zoom. The text-free images are also much smaller: at 150 DPI they were 3% of the full render's size for text pages, about 10% for forms and tables, and around half for photo-heavy pages. The trade-off is that the text is in the browser's fonts, so it looks less like the PDF than the default overlay.

<!-- snippet: RenderHtmlVisibleText -->
<a id='snippet-RenderHtmlVisibleText'></a>
```cs
using var document = PdfiumDocument.Load("multi-page.pdf");
// Visible text over the page image rendered without its text: images and drawings
// stay, and the text is sharp at any zoom
var html = document.RenderHtml(
    new HtmlOptions
    {
        Text = HtmlTextMode.Visible
    });
```
<sup><a href='/src/Morph.PDFium.Tests/Samples.cs#L302-L313' title='Snippet source file'>snippet source</a> | <a href='#snippet-RenderHtmlVisibleText' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`RenderPageHtml` (or `PdfPage.RenderHtml` on an open page) renders one page as a fragment: a `div` carrying the styles it needs inline, to place in any page. For text-only HTML, turn off the image as well.

<!-- snippet: RenderTextHtml -->
<a id='snippet-RenderTextHtml'></a>
```cs
using var document = PdfiumDocument.Load("sample.pdf");
// Text-only HTML: the text in its own color and style, with no page image
var fragment = document.RenderPageHtml(
    0,
    new HtmlOptions
    {
        IncludeImage = false,
        Text = HtmlTextMode.Visible
    });
```
<sup><a href='/src/Morph.PDFium.Tests/Samples.cs#L282-L294' title='Snippet source file'>snippet source</a> | <a href='#snippet-RenderTextHtml' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Only http, https, mailto, ftp and tel links and relative references are kept: `javascript:` links, launch actions and links into embedded files are dropped. For PDFs from an untrusted source, narrow that with `HtmlOptions.AllowedLinkSchemes` and `AllowRelativeLinks`. Links to the document's own pages are kept either way.

<!-- snippet: RenderHtmlUntrustedLinks -->
<a id='snippet-RenderHtmlUntrustedLinks'></a>
```cs
using var document = PdfiumDocument.Load("multi-page.pdf");
// Only web and mail links, and links between the document's pages
var html = document.RenderHtml(
    new HtmlOptions
    {
        AllowedLinkSchemes = ["http", "https", "mailto"],
        AllowRelativeLinks = false
    });
```
<sup><a href='/src/Morph.PDFium.Tests/Samples.cs#L321-L332' title='Snippet source file'>snippet source</a> | <a href='#snippet-RenderHtmlUntrustedLinks' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Extract text

<!-- snippet: ExtractText -->
<a id='snippet-ExtractText'></a>
```cs
using var document = PdfiumDocument.Load("sample.pdf");
using var page = document.LoadPage(0);
var text = page.GetText();
```
<sup><a href='/src/Morph.PDFium.Tests/Samples.cs#L68-L74' title='Snippet source file'>snippet source</a> | <a href='#snippet-ExtractText' title='Start of snippet'>anchor</a></sup>
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
<sup><a href='/src/Morph.PDFium.Tests/Samples.cs#L82-L91' title='Snippet source file'>snippet source</a> | <a href='#snippet-SearchText' title='Start of snippet'>anchor</a></sup>
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
<sup><a href='/src/Morph.PDFium.Tests/Samples.cs#L100-L108' title='Snippet source file'>snippet source</a> | <a href='#snippet-Bookmarks' title='Start of snippet'>anchor</a></sup>
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
<sup><a href='/src/Morph.PDFium.Tests/Samples.cs#L116-L125' title='Snippet source file'>snippet source</a> | <a href='#snippet-Annotations' title='Start of snippet'>anchor</a></sup>
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
<sup><a href='/src/Morph.PDFium.Tests/Samples.cs#L133-L143' title='Snippet source file'>snippet source</a> | <a href='#snippet-FormFields' title='Start of snippet'>anchor</a></sup>
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
<sup><a href='/src/Morph.PDFium.Tests/Samples.cs#L361-L373' title='Snippet source file'>snippet source</a> | <a href='#snippet-MergeDocuments' title='Start of snippet'>anchor</a></sup>
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
<sup><a href='/src/Morph.PDFium.Tests/Samples.cs#L382-L393' title='Snippet source file'>snippet source</a> | <a href='#snippet-EditAndSave' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Embedded file attachments

<!-- snippet: Attachments -->
<a id='snippet-Attachments'></a>
```cs
using var document = PdfiumDocument.Load("sample.pdf");
document.AddAttachment("notes.txt", [.. "embedded data"u8]);
var withAttachment = document.Save();
```
<sup><a href='/src/Morph.PDFium.Tests/Samples.cs#L401-L407' title='Snippet source file'>snippet source</a> | <a href='#snippet-Attachments' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

For the full list of native PDFium entry points that are wrapped (and those intentionally left out), see [native API coverage](docs/native-api-coverage.md).


## Behavior notes

 * Pages render with a white background and annotations included.
 * Output dimensions are `pageSizeInPoints / 72 * dpi`, rounded to the nearest pixel. The default 96 dpi renders an A4 page at 794 x 1123 and a Letter page at 816 x 1056.
 * The PNG includes a `pHYs` chunk recording the render dpi.
 * PDFium is not thread safe, so all native calls are serialized on a process-wide lock. `PdfiumDocument` instances can safely be used from multiple threads. PDFium itself works on one page at a time, but the PNG encoding and other managed work for a page overlap it, which is what `ProcessPages`, `RenderPages` and `RenderHtml` make use of.
 * Rendering is deterministic for a given Morph.PDFium version: for documents whose fonts are embedded, the same input produces the same pixels on every machine and OS, which makes the output suitable for snapshot testing. A font the document does not embed, the standard 14 (Helvetica, Times, Courier and so on) included, is drawn with a matching font from the machine where PDFium finds one, and with its own built-in substitutes otherwise, so text in it can render differently between machines: Helvetica and Times, for instance, come out differently on a Linux machine with the fonts Playwright installs for Chromium than on Windows and macOS. The same goes for the HTML's text widths and link boxes, which follow the glyphs drawn. The PNG bytes also depend on the .NET runtime's deflate, which may change between runtime versions even when the pixels do not. [Verify.PDFium](https://github.com/VerifyTests/Verify.PDFium) builds on this.


## Icon

[PDF](https://thenounproject.com/icon/pdf-7564953//) designed by [Meilia](https://thenounproject.com/creator/meilia1/) from [The Noun Project](https://thenounproject.com).
