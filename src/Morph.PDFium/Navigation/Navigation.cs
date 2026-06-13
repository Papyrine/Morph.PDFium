namespace Morph.PDFium;

/// <summary>The kind of action attached to a bookmark or link (PDF 32000-1:2008, 12.6.4).</summary>
public enum PdfActionType
{
    /// <summary>An action type PDFium does not surface.</summary>
    Unsupported = 0,

    /// <summary>Jump to a destination within the current document.</summary>
    GoTo = 1,

    /// <summary>Jump to a destination within another document (see <see cref="PdfAction.FilePath"/>).</summary>
    RemoteGoTo = 2,

    /// <summary>Resolve a URI (web page or other resource); see <see cref="PdfAction.Uri"/>.</summary>
    Uri = 3,

    /// <summary>Launch an application or open a file (see <see cref="PdfAction.FilePath"/>).</summary>
    Launch = 4,

    /// <summary>Jump to a destination within an embedded file.</summary>
    EmbeddedGoTo = 5
}

/// <summary>
/// A view destination within the document: the target page plus, when the destination uses
/// /XYZ syntax, an optional focus point and zoom.
/// </summary>
/// <param name="PageIndex">Zero-based target page index, or -1 if PDFium could not resolve it.</param>
/// <param name="X">Left page coordinate to scroll to, when specified.</param>
/// <param name="Y">Top page coordinate to scroll to, when specified.</param>
/// <param name="Zoom">Zoom factor to apply, when specified.</param>
public readonly record struct PdfDestination(int PageIndex, double? X, double? Y, double? Zoom);

/// <summary>An action target attached to a bookmark or link.</summary>
/// <param name="Type">The action kind.</param>
/// <param name="Uri">The URI for a <see cref="PdfActionType.Uri"/> action, otherwise null.</param>
/// <param name="FilePath">The file path for launch/remote-goto actions, otherwise null.</param>
/// <param name="Destination">The destination for goto-style actions, otherwise null.</param>
public readonly record struct PdfAction(PdfActionType Type, string? Uri, string? FilePath, PdfDestination? Destination);

/// <summary>
/// Reads PDFium destination/action handles into managed records. Callers must hold
/// <see cref="PdfiumNative.Sync"/>; the handles are only valid for the lifetime of the document.
/// </summary>
static class Navigation
{
    public static PdfDestination? ReadDestination(IntPtr document, IntPtr dest)
    {
        if (dest == IntPtr.Zero)
        {
            return null;
        }

        var pageIndex = PdfiumNative.FPDFDest_GetDestPageIndex(document, dest);
        double? x = null;
        double? y = null;
        double? zoom = null;
        if (PdfiumNative.FPDFDest_GetLocationInPage(dest, out var hasX, out var hasY, out var hasZoom, out var fx, out var fy, out var fzoom))
        {
            if (hasX != 0)
            {
                x = fx;
            }

            if (hasY != 0)
            {
                y = fy;
            }

            if (hasZoom != 0)
            {
                zoom = fzoom;
            }
        }

        return new(pageIndex, x, y, zoom);
    }

    public static PdfAction? ReadAction(IntPtr document, IntPtr action)
    {
        if (action == IntPtr.Zero)
        {
            return null;
        }

        var type = (PdfActionType) PdfiumNative.FPDFAction_GetType(action);
        string? uri = null;
        string? filePath = null;
        PdfDestination? destination = null;

        switch (type)
        {
            case PdfActionType.Uri:
                uri = Interop.Utf8ByLength((buffer, length) => PdfiumNative.FPDFAction_GetURIPath(document, action, buffer, length));
                break;
            case PdfActionType.Launch:
            case PdfActionType.RemoteGoTo:
                filePath = Interop.Utf8ByLength((buffer, length) => PdfiumNative.FPDFAction_GetFilePath(action, buffer, length));
                destination = ReadDestination(document, PdfiumNative.FPDFAction_GetDest(document, action));
                break;
            case PdfActionType.GoTo:
            case PdfActionType.EmbeddedGoTo:
                destination = ReadDestination(document, PdfiumNative.FPDFAction_GetDest(document, action));
                break;
        }

        return new(type, uri, filePath, destination);
    }
}
