namespace Morph.PDFium;

/// <summary>Options controlling how <see cref="PdfiumDocument.Save(SaveFlags,int?)"/> serialises the document.</summary>
[Flags]
public enum SaveFlags : uint
{
    None = 0,

    /// <summary>Append changes as an incremental update rather than rewriting the whole file.</summary>
    Incremental = 1 << 0,

    /// <summary>Force a full (non-incremental) rewrite.</summary>
    NoIncremental = 1 << 1,

    /// <summary>Remove the document's security (encryption) on save.</summary>
    RemoveSecurity = 1 << 2
}
