# Morph.PDFium

Renders PDF pages to PNG images via [PDFium](https://pdfium.googlesource.com/pdfium/), using the prebuilt native binaries from [pdfium-binaries](https://github.com/bblanchon/pdfium-binaries) (Windows, Linux, and macOS). No image library dependency: PNG encoding is built in.

```cs
using var document = PdfiumDocument.Load("input.pdf");

var pageCount = document.PageCount;
var size = document.GetPageSize(0);
var properties = document.GetProperties();

var png = document.RenderPage(0, dpi: 96);
File.WriteAllBytes("page1.png", png);
```

[Documentation](https://github.com/Papyrine/Morph.PDFium)
