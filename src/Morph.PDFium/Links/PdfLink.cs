namespace Morph.PDFium;

/// <summary>A link annotation on a page: a clickable rectangle with a destination or action.</summary>
/// <param name="Rectangle">The link's bounding rectangle in page points.</param>
/// <param name="Destination">The view destination, when the link targets one directly.</param>
/// <param name="Action">The action, when the link carries one instead of a destination.</param>
public readonly record struct PdfLink(PdfRectangle Rectangle, PdfDestination? Destination, PdfAction? Action);

/// <summary>
/// A URL implicitly detected in the page's text (e.g. "https://example.com"), independent of
/// any link annotations the PDF may declare.
/// </summary>
/// <param name="Url">The detected URL.</param>
/// <param name="Rectangles">The rectangles the URL text occupies, in page points.</param>
public sealed record PdfWebLink(string Url, IReadOnlyList<PdfRectangle> Rectangles);
