namespace Morph.PDFium;

/// <summary>Selects which of the two trailer file identifiers to read (PDF 32000-1:2008, 14.4).</summary>
public enum FileIdentifierType
{
    /// <summary>The permanent identifier, assigned when the file is first created.</summary>
    Permanent = 0,

    /// <summary>The changing identifier, updated each time the file is saved.</summary>
    Changing = 1
}
