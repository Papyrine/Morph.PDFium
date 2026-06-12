namespace Morph.PDFium;

public sealed partial class PdfiumDocument
{
    // Malformed documents can contain circular sibling/child references; cap the depth and
    // track visited handles so traversal always terminates.
    const int maxBookmarkDepth = 128;

    /// <summary>
    /// The document outline (bookmarks) as a tree. Returns an empty list when the document
    /// has no outline. The whole tree is materialised eagerly, so it remains valid after the
    /// document is disposed.
    /// </summary>
    public IReadOnlyList<PdfBookmark> GetBookmarks()
    {
        var doc = Handle;
        lock (PdfiumNative.Sync)
        {
            return ReadSiblings(doc, PdfiumNative.FPDFBookmark_GetFirstChild(doc, IntPtr.Zero), [], 0);
        }
    }

    static List<PdfBookmark> ReadSiblings(IntPtr doc, IntPtr bookmark, HashSet<IntPtr> visited, int depth)
    {
        var result = new List<PdfBookmark>();
        while (bookmark != IntPtr.Zero && depth < maxBookmarkDepth && visited.Add(bookmark))
        {
            var title = Interop.Utf16ByLength((buffer, length) => PdfiumNative.FPDFBookmark_GetTitle(bookmark, buffer, length)) ?? string.Empty;
            var destination = Navigation.ReadDestination(doc, PdfiumNative.FPDFBookmark_GetDest(doc, bookmark));
            var action = destination is null ? Navigation.ReadAction(doc, PdfiumNative.FPDFBookmark_GetAction(bookmark)) : null;
            var children = ReadSiblings(doc, PdfiumNative.FPDFBookmark_GetFirstChild(doc, bookmark), visited, depth + 1);
            result.Add(new(title, destination, action, children));
            bookmark = PdfiumNative.FPDFBookmark_GetNextSibling(doc, bookmark);
        }

        return result;
    }
}
