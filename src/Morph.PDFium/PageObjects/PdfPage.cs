namespace Morph.PDFium;

public sealed partial class PdfPage
{
    const int maxStructureDepth = 128;

    /// <summary>Reads the page content objects (text, paths, images, ...) with their bounding boxes.</summary>
    public IReadOnlyList<PdfPageObject> GetObjects()
    {
        var page = ValidHandle();
        lock (PdfiumNative.Sync)
        {
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
    /// using one of the standard 14 fonts. The page content is regenerated so the change is
    /// included on <see cref="PdfiumDocument.Save(SaveFlags,int?)"/>.
    /// </summary>
    public void AddText(string text, double x, double y, string fontName = "Helvetica", double fontSize = 12, PdfColor? color = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        var page = ValidHandle();
        var doc = DocumentHandle;
        var font = Encoding.ASCII.GetBytes(fontName + '\0');
        lock (PdfiumNative.Sync)
        {
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
            PdfiumNative.FPDFPage_InsertObject(page, obj);
            if (!PdfiumNative.FPDFPage_GenerateContent(page))
            {
                throw new PdfiumException($"Failed to regenerate content for page {Index}");
            }
        }
    }

    /// <summary>
    /// Adds a filled (and optionally stroked) rectangle to the page. The page content is
    /// regenerated so the change is included on <see cref="PdfiumDocument.Save(SaveFlags,int?)"/>.
    /// </summary>
    public void AddRectangle(PdfRectangle rectangle, PdfColor fill, PdfColor? stroke = null)
    {
        var page = ValidHandle();
        lock (PdfiumNative.Sync)
        {
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
            PdfiumNative.FPDFPage_InsertObject(page, obj);
            if (!PdfiumNative.FPDFPage_GenerateContent(page))
            {
                throw new PdfiumException($"Failed to regenerate content for page {Index}");
            }
        }
    }

    /// <summary>
    /// Reads the page's logical structure tree (tagged PDF), or an empty list when the page is
    /// not tagged. Useful for accessibility and reading-order extraction.
    /// </summary>
    public IReadOnlyList<PdfStructureElement> GetStructureTree()
    {
        var page = ValidHandle();
        lock (PdfiumNative.Sync)
        {
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
        var type = Interop.Utf16ByLength((buffer, length) => PdfiumNative.FPDF_StructElement_GetType(element, buffer, length));
        var title = Interop.Utf16ByLength((buffer, length) => PdfiumNative.FPDF_StructElement_GetTitle(element, buffer, length));
        var altText = Interop.Utf16ByLength((buffer, length) => PdfiumNative.FPDF_StructElement_GetAltText(element, buffer, length));

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
        var page = ValidHandle();
        lock (PdfiumNative.Sync)
        {
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
