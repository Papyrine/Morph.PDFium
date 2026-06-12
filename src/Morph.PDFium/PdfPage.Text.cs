namespace Morph.PDFium;

/// <summary>A single extracted character with its page geometry.</summary>
/// <param name="Index">Zero-based character index within the page's text stream.</param>
/// <param name="Char">The Unicode character, or '\0' when PDFium has no Unicode mapping.</param>
/// <param name="Box">The character's bounding box in page points.</param>
/// <param name="Origin">The character's drawing origin (baseline) in page points.</param>
/// <param name="FontSize">The typographic font size ("em" size) in points.</param>
public readonly record struct PdfTextChar(int Index, char Char, PdfRectangle Box, PdfPoint Origin, double FontSize);

/// <summary>A run of matching characters produced by <see cref="PdfPage.Search"/>.</summary>
/// <param name="CharIndex">Zero-based index of the first matched character.</param>
/// <param name="CharCount">Number of matched characters.</param>
public readonly record struct PdfTextMatch(int CharIndex, int CharCount);

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

public sealed partial class PdfPage
{
    /// <summary>Number of characters in the page's text stream, including PDFium-generated whitespace.</summary>
    public int CharCount
    {
        get
        {
            var page = ValidHandle();
            lock (PdfiumNative.Sync)
            {
                _ = page;
                return Math.Max(0, PdfiumNative.FPDFText_CountChars(TextHandle()));
            }
        }
    }

    /// <summary>Extracts all text on the page as a single string, or null when the page has no text.</summary>
    public string? GetText()
    {
        var page = ValidHandle();
        lock (PdfiumNative.Sync)
        {
            _ = page;
            var text = TextHandle();
            var count = PdfiumNative.FPDFText_CountChars(text);
            if (count <= 0)
            {
                return null;
            }

            return Interop.Utf16ByUnits(count, (buffer, length) => PdfiumNative.FPDFText_GetText(text, 0, count, buffer[..length]));
        }
    }

    /// <summary>
    /// Extracts the Unicode text contained within the given rectangle (page points), or null
    /// when the rectangle contains no text.
    /// </summary>
    public string? GetTextInRectangle(PdfRectangle rectangle)
    {
        var page = ValidHandle();
        lock (PdfiumNative.Sync)
        {
            _ = page;
            var text = TextHandle();
            var units = PdfiumNative.FPDFText_GetBoundedText(text, rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom, [], 0);
            return Interop.Utf16ByUnits(units,
                (buffer, length) => PdfiumNative.FPDFText_GetBoundedText(text, rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom, buffer[..length], length));
        }
    }

    /// <summary>
    /// Returns per-character detail (Unicode value, bounding box, origin and font size) for
    /// every character on the page, in stream order.
    /// </summary>
    public IReadOnlyList<PdfTextChar> GetChars()
    {
        var page = ValidHandle();
        lock (PdfiumNative.Sync)
        {
            _ = page;
            var text = TextHandle();
            var count = PdfiumNative.FPDFText_CountChars(text);
            if (count <= 0)
            {
                return [];
            }

            var chars = new List<PdfTextChar>(count);
            for (var index = 0; index < count; index++)
            {
                var unicode = PdfiumNative.FPDFText_GetUnicode(text, index);
                var box = PdfiumNative.FPDFText_GetCharBox(text, index, out var left, out var right, out var bottom, out var top)
                    ? new PdfRectangle(left, bottom, right, top)
                    : default;
                var origin = PdfiumNative.FPDFText_GetCharOrigin(text, index, out var x, out var y)
                    ? new PdfPoint(x, y)
                    : default;
                var fontSize = PdfiumNative.FPDFText_GetFontSize(text, index);
                chars.Add(new(index, (char) unicode, box, origin, fontSize));
            }

            return chars;
        }
    }

    /// <summary>
    /// The merged rectangles covering the characters in [<paramref name="startIndex"/>,
    /// <paramref name="startIndex"/> + <paramref name="count"/>). Pass -1 for
    /// <paramref name="count"/> to cover all remaining characters. Useful for highlighting.
    /// </summary>
    public IReadOnlyList<PdfRectangle> GetTextRectangles(int startIndex = 0, int count = -1)
    {
        var page = ValidHandle();
        lock (PdfiumNative.Sync)
        {
            _ = page;
            var text = TextHandle();
            var rectCount = PdfiumNative.FPDFText_CountRects(text, startIndex, count);
            if (rectCount <= 0)
            {
                return [];
            }

            var rectangles = new List<PdfRectangle>(rectCount);
            for (var index = 0; index < rectCount; index++)
            {
                if (PdfiumNative.FPDFText_GetRect(text, index, out var left, out var top, out var right, out var bottom))
                {
                    rectangles.Add(new(left, bottom, right, top));
                }
            }

            return rectangles;
        }
    }

    /// <summary>
    /// The zero-based index of the character at or nearest to (<paramref name="x"/>,
    /// <paramref name="y"/>) within the given tolerance (page points), or -1 if none.
    /// </summary>
    public int GetCharIndexAt(double x, double y, double xTolerance, double yTolerance)
    {
        var page = ValidHandle();
        lock (PdfiumNative.Sync)
        {
            _ = page;
            return PdfiumNative.FPDFText_GetCharIndexAtPos(TextHandle(), x, y, xTolerance, yTolerance);
        }
    }

    /// <summary>
    /// Finds every occurrence of <paramref name="query"/> in the page text. Results are
    /// returned as character ranges that can be passed to <see cref="GetTextRectangles"/>.
    /// </summary>
    public IReadOnlyList<PdfTextMatch> Search(string query, TextSearchOptions options = TextSearchOptions.None)
    {
        ArgumentException.ThrowIfNullOrEmpty(query);
        var page = ValidHandle();
        var pattern = Interop.ToWideString(query);
        var flags = (uint) options; // MatchCase=1, MatchWholeWord=2 line up with PDFium's bits.

        lock (PdfiumNative.Sync)
        {
            _ = page;
            var search = PdfiumNative.FPDFText_FindStart(TextHandle(), pattern, flags, 0);
            if (search == IntPtr.Zero)
            {
                return [];
            }

            try
            {
                var matches = new List<PdfTextMatch>();
                while (PdfiumNative.FPDFText_FindNext(search))
                {
                    matches.Add(new(PdfiumNative.FPDFText_GetSchResultIndex(search), PdfiumNative.FPDFText_GetSchCount(search)));
                }

                return matches;
            }
            finally
            {
                PdfiumNative.FPDFText_FindClose(search);
            }
        }
    }
}
