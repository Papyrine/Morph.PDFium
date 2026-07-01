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
            RegenerateContent(page);
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
            RegenerateContent(page);
        }
    }

    /// <summary>
    /// Adds a straight line from <paramref name="from"/> to <paramref name="to"/> (both in page
    /// points), stroked in <paramref name="color"/>. The page content is regenerated so the change
    /// is included on <see cref="PdfiumDocument.Save(SaveFlags,int?)"/>.
    /// </summary>
    public void AddLine(PdfPoint from, PdfPoint to, PdfColor color, double width = 1)
    {
        var page = ValidHandle();
        lock (PdfiumNative.Sync)
        {
            var path = PdfiumNative.FPDFPageObj_CreateNewPath((float) from.X, (float) from.Y);
            if (path == IntPtr.Zero)
            {
                throw new PdfiumException("Failed to create path object");
            }

            if (!PdfiumNative.FPDFPath_LineTo(path, (float) to.X, (float) to.Y))
            {
                PdfiumNative.FPDFPageObj_Destroy(path);
                throw new PdfiumException("Failed to add line segment");
            }

            PdfiumNative.FPDFPageObj_SetStrokeColor(path, color.R, color.G, color.B, color.A);
            PdfiumNative.FPDFPageObj_SetStrokeWidth(path, (float) width);
            PdfiumNative.FPDFPath_SetDrawMode(path, PdfiumNative.FillModeNone, stroke: true);
            PdfiumNative.FPDFPage_InsertObject(page, path);
            RegenerateContent(page);
        }
    }

    /// <summary>
    /// Adds a path connecting <paramref name="points"/> (in page points) with straight segments,
    /// filled and/or stroked. Supply a <paramref name="fill"/> color, a <paramref name="stroke"/>
    /// color, or both; <paramref name="close"/> joins the last point back to the first. The page
    /// content is regenerated so the change is included on <see cref="PdfiumDocument.Save(SaveFlags,int?)"/>.
    /// </summary>
    public void AddPath(IReadOnlyList<PdfPoint> points, PdfColor? fill = null, PdfColor? stroke = null, double strokeWidth = 1, bool close = true)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count < 2)
        {
            throw new ArgumentException("A path needs at least two points", nameof(points));
        }

        if (fill is null && stroke is null)
        {
            throw new ArgumentException("Specify a fill color, a stroke color, or both", nameof(fill));
        }

        var page = ValidHandle();
        lock (PdfiumNative.Sync)
        {
            var path = PdfiumNative.FPDFPageObj_CreateNewPath((float) points[0].X, (float) points[0].Y);
            if (path == IntPtr.Zero)
            {
                throw new PdfiumException("Failed to create path object");
            }

            try
            {
                for (var index = 1; index < points.Count; index++)
                {
                    if (!PdfiumNative.FPDFPath_LineTo(path, (float) points[index].X, (float) points[index].Y))
                    {
                        throw new PdfiumException("Failed to add path segment");
                    }
                }

                if (close && !PdfiumNative.FPDFPath_Close(path))
                {
                    throw new PdfiumException("Failed to close path");
                }
            }
            catch
            {
                PdfiumNative.FPDFPageObj_Destroy(path);
                throw;
            }

            if (fill is { } f)
            {
                PdfiumNative.FPDFPageObj_SetFillColor(path, f.R, f.G, f.B, f.A);
            }

            if (stroke is { } s)
            {
                PdfiumNative.FPDFPageObj_SetStrokeColor(path, s.R, s.G, s.B, s.A);
                PdfiumNative.FPDFPageObj_SetStrokeWidth(path, (float) strokeWidth);
            }

            PdfiumNative.FPDFPath_SetDrawMode(path, fill is null ? PdfiumNative.FillModeNone : PdfiumNative.FillModeWinding, stroke is not null);
            PdfiumNative.FPDFPage_InsertObject(page, path);
            RegenerateContent(page);
        }
    }

    /// <summary>
    /// Adds an image, scaling the <paramref name="pixelWidth"/>×<paramref name="pixelHeight"/>
    /// pixel buffer to fill <paramref name="destination"/> (in page points). <paramref name="pixels"/>
    /// is top-down RGBA, 8 bits per channel (4 bytes per pixel, row stride <c>pixelWidth * 4</c>).
    /// The page content is regenerated so the change is included on <see cref="PdfiumDocument.Save(SaveFlags,int?)"/>.
    /// </summary>
    public void AddImage(ReadOnlySpan<byte> pixels, int pixelWidth, int pixelHeight, PdfRectangle destination)
    {
        if (pixelWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelWidth), pixelWidth, "Width must be positive");
        }

        if (pixelHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelHeight), pixelHeight, "Height must be positive");
        }

        var required = pixelWidth * pixelHeight * 4;
        if (pixels.Length < required)
        {
            throw new ArgumentException($"Expected at least {required} bytes of RGBA pixel data, got {pixels.Length}", nameof(pixels));
        }

        if (destination.Width <= 0 || destination.Height <= 0)
        {
            throw new ArgumentException("Destination rectangle must have positive width and height", nameof(destination));
        }

        var page = ValidHandle();
        var doc = DocumentHandle;

        // Convert the caller's RGBA to the BGRA byte order PDFium's bitmaps use.
        var bgra = new byte[required];
        for (var pixel = 0; pixel < pixelWidth * pixelHeight; pixel++)
        {
            var offset = pixel * 4;
            bgra[offset] = pixels[offset + 2];
            bgra[offset + 1] = pixels[offset + 1];
            bgra[offset + 2] = pixels[offset];
            bgra[offset + 3] = pixels[offset + 3];
        }

        lock (PdfiumNative.Sync)
        {
            var bitmap = PdfiumNative.FPDFBitmap_Create(pixelWidth, pixelHeight, 1);
            if (bitmap == IntPtr.Zero)
            {
                throw new PdfiumException("Failed to allocate image bitmap");
            }

            try
            {
                var buffer = PdfiumNative.FPDFBitmap_GetBuffer(bitmap);
                if (buffer == IntPtr.Zero)
                {
                    throw new PdfiumException("Failed to access image bitmap buffer");
                }

                // A 32-bit bitmap has no row padding, so the buffer is a contiguous block.
                Marshal.Copy(bgra, 0, buffer, bgra.Length);

                var image = PdfiumNative.FPDFPageObj_NewImageObj(doc);
                if (image == IntPtr.Zero)
                {
                    throw new PdfiumException("Failed to create image object");
                }

                if (!PdfiumNative.FPDFImageObj_SetBitmap([page], 1, image, bitmap))
                {
                    PdfiumNative.FPDFPageObj_Destroy(image);
                    throw new PdfiumException("Failed to set image bitmap");
                }

                // An image object maps the unit square, so scale/translate it onto the destination.
                PdfiumNative.FPDFPageObj_Transform(image, destination.Width, 0, 0, destination.Height, destination.Left, destination.Bottom);
                PdfiumNative.FPDFPage_InsertObject(page, image);
                RegenerateContent(page);
            }
            finally
            {
                PdfiumNative.FPDFBitmap_Destroy(bitmap);
            }
        }
    }

    /// <summary>
    /// Removes the page object at <paramref name="index"/> (as reported by <see cref="GetObjects"/>).
    /// Removing an object shifts the indices of those after it. The page content is regenerated so
    /// the change is included on <see cref="PdfiumDocument.Save(SaveFlags,int?)"/>.
    /// </summary>
    public void RemoveObject(int index)
    {
        var page = ValidHandle();
        lock (PdfiumNative.Sync)
        {
            var obj = ObjectAt(page, index);
            if (!PdfiumNative.FPDFPage_RemoveObject(page, obj))
            {
                throw new PdfiumException($"Failed to remove page object {index}");
            }

            // FPDFPage_RemoveObject detaches the object but transfers ownership back to us.
            PdfiumNative.FPDFPageObj_Destroy(obj);
            RegenerateContent(page);
        }
    }

    /// <summary>Sets the fill color of the page object at <paramref name="index"/>.</summary>
    public void SetObjectFillColor(int index, PdfColor color)
    {
        var page = ValidHandle();
        lock (PdfiumNative.Sync)
        {
            var obj = ObjectAt(page, index);
            if (!PdfiumNative.FPDFPageObj_SetFillColor(obj, color.R, color.G, color.B, color.A))
            {
                throw new PdfiumException($"Failed to set fill color on page object {index}");
            }

            RegenerateContent(page);
        }
    }

    /// <summary>Sets the stroke color of the page object at <paramref name="index"/>.</summary>
    public void SetObjectStrokeColor(int index, PdfColor color)
    {
        var page = ValidHandle();
        lock (PdfiumNative.Sync)
        {
            var obj = ObjectAt(page, index);
            if (!PdfiumNative.FPDFPageObj_SetStrokeColor(obj, color.R, color.G, color.B, color.A))
            {
                throw new PdfiumException($"Failed to set stroke color on page object {index}");
            }

            RegenerateContent(page);
        }
    }

    /// <summary>Sets the stroke width (in points) of the page object at <paramref name="index"/>.</summary>
    public void SetObjectStrokeWidth(int index, double width)
    {
        var page = ValidHandle();
        lock (PdfiumNative.Sync)
        {
            var obj = ObjectAt(page, index);
            if (!PdfiumNative.FPDFPageObj_SetStrokeWidth(obj, (float) width))
            {
                throw new PdfiumException($"Failed to set stroke width on page object {index}");
            }

            RegenerateContent(page);
        }
    }

    /// <summary>Moves the page object at <paramref name="index"/> by (<paramref name="dx"/>, <paramref name="dy"/>) points.</summary>
    public void MoveObject(int index, double dx, double dy) =>
        TransformObject(index, 1, 0, 0, 1, dx, dy);

    /// <summary>
    /// Applies the affine transform <c>[a b c d e f]</c> to the page object at <paramref name="index"/>,
    /// composing with its current matrix (so translation is in points, and scale/rotation are relative
    /// to the object's existing placement). The page content is regenerated so the change is included
    /// on <see cref="PdfiumDocument.Save(SaveFlags,int?)"/>.
    /// </summary>
    public void TransformObject(int index, double a, double b, double c, double d, double e, double f)
    {
        var page = ValidHandle();
        lock (PdfiumNative.Sync)
        {
            var obj = ObjectAt(page, index);
            PdfiumNative.FPDFPageObj_Transform(obj, a, b, c, d, e, f);
            RegenerateContent(page);
        }
    }

    // Caller must hold PdfiumNative.Sync. Fetches the object handle, throwing for an out-of-range index.
    static IntPtr ObjectAt(IntPtr page, int index)
    {
        var obj = PdfiumNative.FPDFPage_GetObject(page, index);
        if (obj == IntPtr.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "No page object at the given index");
        }

        return obj;
    }

    // Caller must hold PdfiumNative.Sync. Rewrites the content stream so edits persist on save.
    void RegenerateContent(IntPtr page)
    {
        if (!PdfiumNative.FPDFPage_GenerateContent(page))
        {
            throw new PdfiumException($"Failed to regenerate content for page {Index}");
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
