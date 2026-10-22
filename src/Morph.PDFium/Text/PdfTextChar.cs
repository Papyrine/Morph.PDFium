namespace Morph.PDFium;

/// <summary>A single extracted character with its page geometry.</summary>
/// <param name="Index">Zero-based character index within the page's text stream.</param>
/// <param name="Char">
/// The Unicode character, '\0' when PDFium has no Unicode mapping, or U+FFFD when the value
/// does not fit a single UTF-16 code unit (see <see cref="CodePoint"/>).
/// </param>
/// <param name="Box">The character's bounding box in page points.</param>
/// <param name="Origin">The character's drawing origin (baseline) in page points.</param>
/// <param name="FontSize">The typographic font size ("em" size) in points.</param>
public readonly record struct PdfTextChar(int Index, char Char, PdfRectangle Box, PdfPoint Origin, double FontSize)
{
    /// <summary>
    /// The Unicode value exactly as PDFium reports it. On every platform PDFium reports a code
    /// point beyond the Basic Multilingual Plane as two characters, the halves of its
    /// surrogate pair, each with a <see cref="CodePoint"/> of its own; one above 0xFFFF is
    /// shown as U+FFFD in <see cref="Char"/>.
    /// </summary>
    public int CodePoint { get; init; }
}
