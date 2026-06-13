namespace Morph.PDFium;

/// <summary>
/// A URL implicitly detected in the page's text (e.g. "https://example.com"), independent of
/// any link annotations the PDF may declare.
/// </summary>
/// <param name="Url">The detected URL.</param>
/// <param name="Rectangles">The rectangles the URL text occupies, in page points.</param>
public sealed record PdfWebLink(string Url, IReadOnlyList<PdfRectangle> Rectangles);
