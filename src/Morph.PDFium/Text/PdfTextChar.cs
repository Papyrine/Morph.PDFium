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
    /// The Unicode value exactly as PDFium reports it. PDFium's characters are 32 bit outside
    /// Windows, so there this can be a code point beyond the Basic Multilingual Plane, which
    /// <see cref="Char"/> cannot hold.
    /// </summary>
    public int CodePoint { get; init; }
}
