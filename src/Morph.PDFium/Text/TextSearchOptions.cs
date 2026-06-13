namespace Morph.PDFium;

/// <summary>Options controlling how <see cref="PdfPage.Search"/> matches.</summary>
[Flags]
public enum TextSearchOptions
{
    None = 0,

    /// <summary>Match is case sensitive.</summary>
    MatchCase = 1,

    /// <summary>Match whole words only.</summary>
    MatchWholeWord = 2
}
