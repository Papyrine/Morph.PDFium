namespace Morph.PDFium;

/// <summary>An RGBA color with 8 bits per channel.</summary>
public readonly record struct PdfColor(byte R, byte G, byte B, byte A);

/// <summary>Annotation subtypes (PDF 32000-1:2008, Table 169), as reported by PDFium.</summary>
public enum PdfAnnotationType
{
    Unknown = 0,
    Text = 1,
    Link = 2,
    FreeText = 3,
    Line = 4,
    Square = 5,
    Circle = 6,
    Polygon = 7,
    Polyline = 8,
    Highlight = 9,
    Underline = 10,
    Squiggly = 11,
    StrikeOut = 12,
    Stamp = 13,
    Caret = 14,
    Ink = 15,
    Popup = 16,
    FileAttachment = 17,
    Sound = 18,
    Movie = 19,
    Widget = 20,
    Screen = 21,
    PrinterMark = 22,
    TrapNet = 23,
    Watermark = 24,
    ThreeD = 25,
    RichMedia = 26,
    XfaWidget = 27,
    Redact = 28
}

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
