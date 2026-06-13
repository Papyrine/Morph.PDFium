namespace Morph.PDFium;

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
