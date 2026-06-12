namespace Morph.PDFium;

/// <summary>
/// User access permission bits from a PDF's encryption dictionary (PDF 32000-1:2008,
/// Table 22). When a document is unprotected or was opened with the owner password,
/// PDFium reports all permissions granted (<see cref="All"/>).
/// </summary>
[Flags]
public enum DocumentPermissions : uint
{
    None = 0,

    /// <summary>(Bit 3) Print the document, possibly at degraded quality — see <see cref="PrintHighQuality"/>.</summary>
    Print = 1u << 2,

    /// <summary>(Bit 4) Modify the document's contents other than via the rights below.</summary>
    Modify = 1u << 3,

    /// <summary>(Bit 5) Copy or otherwise extract text and graphics.</summary>
    ExtractContent = 1u << 4,

    /// <summary>(Bit 6) Add or modify text annotations and fill in interactive form fields.</summary>
    ModifyAnnotations = 1u << 5,

    /// <summary>(Bit 9) Fill in existing interactive form fields, even without <see cref="ModifyAnnotations"/>.</summary>
    FillForms = 1u << 8,

    /// <summary>(Bit 10) Extract text and graphics for accessibility purposes.</summary>
    ExtractForAccessibility = 1u << 9,

    /// <summary>(Bit 11) Assemble the document (insert, rotate, delete pages, add bookmarks).</summary>
    Assemble = 1u << 10,

    /// <summary>(Bit 12) Print at full quality. Without this, printing is limited to a low-resolution image.</summary>
    PrintHighQuality = 1u << 11,

    /// <summary>All bits set, as reported for an unprotected or owner-unlocked document.</summary>
    All = 0xFFFFFFFF
}

/// <summary>The page mode that specifies how a document should be displayed when opened (PDF catalog /PageMode).</summary>
public enum PageDisplayMode
{
    /// <summary>PDFium could not determine the page mode.</summary>
    Unknown = -1,

    /// <summary>Neither outline nor thumbnail panel visible.</summary>
    UseNone = 0,

    /// <summary>Show the document outline (bookmarks) panel.</summary>
    UseOutlines = 1,

    /// <summary>Show the page thumbnail panel.</summary>
    UseThumbnails = 2,

    /// <summary>Open in full-screen mode, with no menu bar, window controls or other windows visible.</summary>
    FullScreen = 3,

    /// <summary>Show the optional content group (layers) panel.</summary>
    UseOptionalContent = 4,

    /// <summary>Show the attachments panel.</summary>
    UseAttachments = 5
}

/// <summary>Selects which of the two trailer file identifiers to read (PDF 32000-1:2008, 14.4).</summary>
public enum FileIdentifierType
{
    /// <summary>The permanent identifier, assigned when the file is first created.</summary>
    Permanent = 0,

    /// <summary>The changing identifier, updated each time the file is saved.</summary>
    Changing = 1
}
