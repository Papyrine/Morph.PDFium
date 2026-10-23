namespace Morph.PDFium;

/// <summary>The kind of interactive form a document carries.</summary>
public enum FormType
{
    /// <summary>The document has no interactive form.</summary>
    None = 0,

    /// <summary>An AcroForm (the standard PDF interactive form).</summary>
    AcroForm = 1,

    /// <summary>A full XFA form (not supported by the default PDFium build).</summary>
    XfaFull = 2,

    /// <summary>An XFA foreground form (not supported by the default PDFium build).</summary>
    XfaForeground = 3
}
