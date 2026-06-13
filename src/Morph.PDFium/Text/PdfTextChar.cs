namespace Morph.PDFium;

/// <summary>A single extracted character with its page geometry.</summary>
/// <param name="Index">Zero-based character index within the page's text stream.</param>
/// <param name="Char">The Unicode character, or '\0' when PDFium has no Unicode mapping.</param>
/// <param name="Box">The character's bounding box in page points.</param>
/// <param name="Origin">The character's drawing origin (baseline) in page points.</param>
/// <param name="FontSize">The typographic font size ("em" size) in points.</param>
public readonly record struct PdfTextChar(int Index, char Char, PdfRectangle Box, PdfPoint Origin, double FontSize);
