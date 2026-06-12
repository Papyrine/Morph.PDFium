# <img src="/src/icon.png" height="30px"> Morph.PDFium

[![Build status](https://img.shields.io/appveyor/build/SimonCropp/Morph-PDFium)](https://ci.appveyor.com/project/SimonCropp/Morph-PDFium)
[![NuGet Status](https://img.shields.io/nuget/v/Morph.PDFium.svg?label=Morph.PDFium)](https://www.nuget.org/packages/Morph.PDFium/)

Renders PDF pages to PNG images via [PDFium](https://pdfium.googlesource.com/pdfium/), using the prebuilt native binaries from [pdfium-binaries](https://github.com/bblanchon/pdfium-binaries) (Windows, Linux, and macOS). No image library dependency: PNG encoding is built in.

**See [Milestones](../../milestones?state=closed) for release notes.**


## Open Source Maintenance Fee

This project participates in the [Open Source Maintenance Fee](https://opensourcemaintenancefee.org). The source code is freely available under the terms of the [license](license.txt). To support sustainable maintenance, use of the project's official binary releases in revenue-generating activities and all government agencies requires adherence to the [Open Source Maintenance Fee EULA](OsmfEula.txt). The fee is paid by [sponsoring Papyrine](https://github.com/sponsors/Papyrine).

This project uses [SponsorCheck](https://github.com/SimonCropp/SponsorCheck) to surface a build-time reminder in consuming projects that are not yet sponsoring.


## NuGet package

[Morph.PDFium](https://www.nuget.org/packages/Morph.PDFium/)


## Usage


### Render a page

<!-- snippet: RenderPage -->
<a id='snippet-RenderPage'></a>
```cs
[Test]
public async Task RenderPage()
{
    using var document = PdfiumDocument.Load("sample.pdf");
    var png = document.RenderPage(0, dpi: 96);
    await Verify(png, "png");
}
```
<sup><a href='/src/Morph.PDFium.Tests/Samples.cs#L3-L13' title='Snippet source file'>snippet source</a> | <a href='#snippet-RenderPage' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Render all pages

<!-- snippet: RenderPages -->
<a id='snippet-RenderPages'></a>
```cs
[Test]
public async Task RenderPages()
{
    using var document = PdfiumDocument.Load("multi-page.pdf");
    var targets = document.RenderPages()
        .Select((_, index) => new Target("png", new MemoryStream(_), $"page_{index + 1:0000}"))
        .ToList();
    await Verify(targets);
}
```
<sup><a href='/src/Morph.PDFium.Tests/Samples.cs#L15-L27' title='Snippet source file'>snippet source</a> | <a href='#snippet-RenderPages' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Document metadata

<!-- snippet: DocumentInfo -->
<a id='snippet-DocumentInfo'></a>
```cs
[Test]
public async Task DocumentInfo()
{
    using var document = PdfiumDocument.Load("multi-page.pdf");
    await Verify(
        new
        {
            document.PageCount,
            Sizes = document.GetPageSizes(),
            Properties = document.GetProperties()
        });
}
```
<sup><a href='/src/Morph.PDFium.Tests/Samples.cs#L29-L44' title='Snippet source file'>snippet source</a> | <a href='#snippet-DocumentInfo' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


## Behavior notes

 * Pages render with a white background and annotations included.
 * Output dimensions are `pageSizeInPoints / 72 * dpi`, rounded to the nearest pixel. The default 96 dpi renders an A4 page at 794 x 1123 and a Letter page at 816 x 1056.
 * The PNG includes a `pHYs` chunk recording the render dpi.
 * PDFium is not thread safe, so all native calls are serialized on a process-wide lock. `PdfiumDocument` instances can safely be used from multiple threads, but rendering does not parallelize.
 * Rendering is deterministic for a given Morph.PDFium version: the same input produces byte-identical PNGs on every machine and OS, which makes the output suitable for snapshot testing. [Verify.PDFium](https://github.com/VerifyTests/Verify.PDFium) builds on this.


## Icon

[Impossible Star](https://thenounproject.com/icon/impossible-star-3612694/) designed by [Rflor](https://thenounproject.com/creator/rflor/) from [The Noun Project](https://thenounproject.com).
