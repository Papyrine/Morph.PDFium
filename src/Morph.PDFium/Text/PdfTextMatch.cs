namespace Morph.PDFium;

/// <summary>A run of matching characters produced by <see cref="PdfPage.Search"/>.</summary>
/// <param name="CharIndex">Zero-based index of the first matched character.</param>
/// <param name="CharCount">Number of matched characters.</param>
public readonly record struct PdfTextMatch(int CharIndex, int CharCount);
