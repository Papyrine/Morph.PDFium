namespace Morph.PDFium;

/// <summary>
/// A page annotation read into a managed record: its type, rectangle, text contents and
/// color. To create or remove annotations use <see cref="PdfPage.AddAnnotation"/> and
/// <see cref="PdfPage.RemoveAnnotation"/>.
/// </summary>
/// <param name="Index">The annotation's index on the page.</param>
/// <param name="Type">The annotation subtype.</param>
/// <param name="Rectangle">The annotation's bounding rectangle in page points.</param>
/// <param name="Contents">The annotation's text contents (the /Contents entry), when present.</param>
/// <param name="Color">The annotation's color (the /C entry), when present.</param>
public readonly record struct PdfAnnotation(int Index, PdfAnnotationType Type, PdfRectangle Rectangle, string? Contents, PdfColor? Color);
