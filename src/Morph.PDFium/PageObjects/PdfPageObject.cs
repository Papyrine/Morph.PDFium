namespace Morph.PDFium;

/// <summary>The kind of a page content object.</summary>
public enum PdfPageObjectType
{
    Unknown = 0,
    Text = 1,
    Path = 2,
    Image = 3,
    Shading = 4,
    Form = 5
}

/// <summary>A page content object read into a managed record.</summary>
/// <param name="Index">The object's index within the page.</param>
/// <param name="Type">The object kind.</param>
/// <param name="Bounds">The object's bounding box in page points.</param>
public readonly record struct PdfPageObject(int Index, PdfPageObjectType Type, PdfRectangle Bounds);

/// <summary>A node in a tagged PDF's logical structure tree.</summary>
/// <param name="Type">The structure type (/S), e.g. "P", "H1", "Figure".</param>
/// <param name="Title">The element title (/T), when present.</param>
/// <param name="AltText">The alternate text (/Alt) for accessibility, when present.</param>
/// <param name="Children">Nested structure elements.</param>
public sealed record PdfStructureElement(string? Type, string? Title, string? AltText, IReadOnlyList<PdfStructureElement> Children);
