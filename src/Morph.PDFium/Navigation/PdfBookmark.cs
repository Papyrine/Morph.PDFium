namespace Morph.PDFium;

/// <summary>
/// A document outline entry. Children are read eagerly into a tree, so the whole outline is
/// available after the owning document is disposed.
/// </summary>
/// <param name="Title">The bookmark label.</param>
/// <param name="Destination">The view destination, when the bookmark targets one directly.</param>
/// <param name="Action">The action, when the bookmark carries one instead of a destination.</param>
/// <param name="Children">Nested bookmarks.</param>
public sealed record PdfBookmark(string Title, PdfDestination? Destination, PdfAction? Action, IReadOnlyList<PdfBookmark> Children);
