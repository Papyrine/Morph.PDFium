namespace Morph.PDFium;

public sealed partial class PdfPage
{
    const int maxStructureDepth = 128;

    /// <summary>Reads the page content objects (text, paths, images, ...) with their bounding boxes.</summary>
    public IReadOnlyList<PdfPageObject> GetObjects()
    {
        lock (PdfiumNative.Sync)
        {
            var page = ValidHandle();
            var count = PdfiumNative.FPDFPage_CountObjects(page);
            if (count <= 0)
            {
                return [];
            }

            var objects = new List<PdfPageObject>(count);
            for (var index = 0; index < count; index++)
            {
                var obj = PdfiumNative.FPDFPage_GetObject(page, index);
                if (obj == IntPtr.Zero)
                {
                    continue;
                }

                var type = (PdfPageObjectType) PdfiumNative.FPDFPageObj_GetType(obj);
                var bounds = PdfiumNative.FPDFPageObj_GetBounds(obj, out var left, out var bottom, out var right, out var top)
                    ? new PdfRectangle(left, bottom, right, top)
                    : default;
                objects.Add(new(index, type, bounds));
            }

            return objects;
        }
    }

    /// <summary>
    /// Adds a text object drawn at (<paramref name="x"/>, <paramref name="y"/>) in page points,
    /// using one of the standard 14 fonts. The page content stream is regenerated once, when
    /// it is next needed (save, render or disposing the page), not on every call. The text
    /// fonts cover the printable Windows-1252 (Western European) characters, without the
    /// no-break space and soft hyphen; text containing anything else, a control character
    /// such as a tab or line break included, throws <see cref="ArgumentException"/>. Symbol
    /// and ZapfDingbats take the characters of their own sets (Greek letters and mathematical
    /// signs, and dingbats) and are not checked: any other character is written as ÿ.
    /// </summary>
    public void AddText(string text, double x, double y, string fontName = "Helvetica", double fontSize = 12, PdfColor? color = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        ThrowIfNotEncodable(text, fontName);
        var font =Encoding.ASCII.GetBytes(fontName + '\0');
        lock (PdfiumNative.Sync)
        {
            var page = ValidHandle();
            var doc = DocumentHandle;
            var obj = PdfiumNative.FPDFPageObj_NewTextObj(doc, font, (float) fontSize);
            if (obj == IntPtr.Zero)
            {
                throw new PdfiumException($"Failed to create text object (unknown font '{fontName}'?)");
            }

            if (!PdfiumNative.FPDFText_SetText(obj, Interop.ToWideString(text)))
            {
                PdfiumNative.FPDFPageObj_Destroy(obj);
                throw new PdfiumException("Failed to set text on the new text object");
            }

            var fill = color ?? new PdfColor(0, 0, 0, 255);
            PdfiumNative.FPDFPageObj_SetFillColor(obj, fill.R, fill.G, fill.B, fill.A);
            // Translate the object to the requested baseline position.
            PdfiumNative.FPDFPageObj_Transform(obj, 1, 0, 0, 1, x, y);
            if (!PdfiumNative.FPDFPage_InsertObject(page, obj))
            {
                // Still ours to free: the page took no ownership of it.
                PdfiumNative.FPDFPageObj_Destroy(obj);
                throw new PdfiumException("Failed to add the object to the page");
            }

            contentPending = true;
            CloseText();
        }
    }

    // The characters Windows-1252 has beyond Latin-1 (its 0x80 to 0x9F range): the euro sign,
    // curly quotes, dashes and so on.
    const string windows1252Extras = "€‚ƒ„…†‡ˆ‰Š‹ŒŽ‘’“”•–—˜™š›œžŸ";

    // The standard fonts are single byte fonts covering Windows-1252. PDFium does not report
    // a character outside that set: it writes a different one in its place, so the saved
    // text is silently wrong. Reject such text up front instead.
    static void ThrowIfNotEncodable(string text, string fontName)
    {
        // These two are symbol sets with encodings of their own, not text fonts.
        if (fontName is "Symbol" or "ZapfDingbats")
        {
            return;
        }

        foreach (var character in text)
        {
            // Printable ASCII and Latin-1, and the extras. Not the control characters, C0 or
            // C1, nor Latin-1's no-break space or soft hyphen: PDFium writes each of those as ÿ.
            var encodable = character is >= ' ' and <= '~' or >= '¡' and <= 'ÿ' && character != '\u00AD';
            if (!encodable && !windows1252Extras.Contains(character))
            {
                throw new ArgumentException($"Character '{character}' (U+{(int) character:X4}) cannot be written with font '{fontName}'. The standard text fonts only cover printable Windows-1252.", nameof(text));
            }
        }
    }

    /// <summary>
    /// Adds a filled (and optionally stroked) rectangle to the page. The page content stream
    /// is regenerated once, when it is next needed (save, render or disposing the page), not
    /// on every call.
    /// </summary>
    public void AddRectangle(PdfRectangle rectangle, PdfColor fill, PdfColor? stroke = null)
    {
        lock (PdfiumNative.Sync)
        {
            var page = ValidHandle();
            var obj = PdfiumNative.FPDFPageObj_CreateNewRect((float) rectangle.Left, (float) rectangle.Bottom, (float) rectangle.Width, (float) rectangle.Height);
            if (obj == IntPtr.Zero)
            {
                throw new PdfiumException("Failed to create rectangle object");
            }

            PdfiumNative.FPDFPageObj_SetFillColor(obj, fill.R, fill.G, fill.B, fill.A);
            if (stroke is { } s)
            {
                PdfiumNative.FPDFPageObj_SetStrokeColor(obj, s.R, s.G, s.B, s.A);
            }

            // A path only fills/strokes once its draw mode is set; without this the rectangle
            // is a no-op and PDFium discards it when the content stream is reparsed on load.
            PdfiumNative.FPDFPath_SetDrawMode(obj, PdfiumNative.FillModeWinding, stroke is not null);
            if (!PdfiumNative.FPDFPage_InsertObject(page, obj))
            {
                // Still ours to free: the page took no ownership of it.
                PdfiumNative.FPDFPageObj_Destroy(obj);
                throw new PdfiumException("Failed to add the object to the page");
            }

            contentPending = true;
        }
    }

    /// <summary>
    /// Reads the page's logical structure tree (tagged PDF), or an empty list when the page is
    /// not tagged. Useful for accessibility and reading-order extraction.
    /// </summary>
    public IReadOnlyList<PdfStructureElement> GetStructureTree()
    {
        lock (PdfiumNative.Sync)
        {
            var page = ValidHandle();
            var tree = PdfiumNative.FPDF_StructTree_GetForPage(page);
            if (tree == IntPtr.Zero)
            {
                return [];
            }

            try
            {
                var count = PdfiumNative.FPDF_StructTree_CountChildren(tree);
                var roots = new List<PdfStructureElement>(Math.Max(0, count));
                for (var index = 0; index < count; index++)
                {
                    var element = PdfiumNative.FPDF_StructTree_GetChildAtIndex(tree, index);
                    if (element != IntPtr.Zero)
                    {
                        roots.Add(ReadStructureElement(element, 0));
                    }
                }

                return roots;
            }
            finally
            {
                PdfiumNative.FPDF_StructTree_Close(tree);
            }
        }
    }

    static PdfStructureElement ReadStructureElement(IntPtr element, int depth)
    {
        var type = Interop.Utf16ByLength(element, static (state, buffer, length) => PdfiumNative.FPDF_StructElement_GetType(state, buffer, length));
        var title = Interop.Utf16ByLength(element, static (state, buffer, length) => PdfiumNative.FPDF_StructElement_GetTitle(state, buffer, length));
        var altText = Interop.Utf16ByLength(element, static (state, buffer, length) => PdfiumNative.FPDF_StructElement_GetAltText(state, buffer, length));

        var children = new List<PdfStructureElement>();
        if (depth < maxStructureDepth)
        {
            var count = PdfiumNative.FPDF_StructElement_CountChildren(element);
            for (var index = 0; index < count; index++)
            {
                var child = PdfiumNative.FPDF_StructElement_GetChildAtIndex(element, index);
                if (child != IntPtr.Zero)
                {
                    children.Add(ReadStructureElement(child, depth + 1));
                }
            }
        }

        return new(type, title, altText, children);
    }

    /// <summary>
    /// The page's embedded thumbnail image as decoded image bytes (typically a JPEG/PNG/raw
    /// stream), or null when the page has no embedded thumbnail.
    /// </summary>
    public byte[]? GetEmbeddedThumbnail()
    {
        lock (PdfiumNative.Sync)
        {
            var page = ValidHandle();
            var length = PdfiumNative.FPDFPage_GetDecodedThumbnailData(page, [], 0);
            if (length == 0)
            {
                return null;
            }

            var buffer = new byte[length];
            PdfiumNative.FPDFPage_GetDecodedThumbnailData(page, buffer, length);
            return buffer;
        }
    }
}
