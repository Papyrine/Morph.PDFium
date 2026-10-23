using Morph.PDFium;

// Running headers and footers as a tagged PDF marks them: content in a marked-content sequence
// tagged /Artifact whose properties are /Type /Pagination with /Subtype /Header or /Footer, as
// Word and other tagged-PDF producers write them. Other artifacts and untagged content are not
// headers or footers here, whatever they look like. Every method assumes the caller holds
// PdfiumNative.Sync.
static class HeaderFooterArtifacts
{
    // Form XObjects nest; malformed ones could nest without end.
    const int maxFormDepth = 32;

    // The share of the displayed page's height, at its top and at its bottom, that a
    // pagination artifact whose subtype PDFium cannot read (see Classify) must lie wholly
    // within to count as a header or footer. One that reaches into the body, such as a
    // watermark across the page, is not one.
    public const double MarginBand = 0.15;

    static readonly FsMatrix identity = new()
    {
        A = 1,
        D = 1
    };

    // Calls found for each header or footer object on the page, with its bounds on the page.
    // An object is passed at the outermost level it is marked: a form XObject drawn within a
    // header is passed whole, not its objects, whose marks start afresh in the form's own
    // content. Within a form that is not one, its objects are looked at in turn, into nested
    // forms 32 deep. display maps the page onto the displayed page, height high.
    public static void Find(IntPtr page, DisplayMatrix display, double height, Action<IntPtr, PdfRectangle?> found)
    {
        var count = PdfiumNative.FPDFPage_CountObjects(page);
        for (var index = 0; index < count; index++)
        {
            Find(PdfiumNative.FPDFPage_GetObject(page, index), identity, 0, display, height, found);
        }
    }

    static void Find(IntPtr pageObject, FsMatrix toPage, int depth, DisplayMatrix display, double height, Action<IntPtr, PdfRectangle?> found)
    {
        if (pageObject == IntPtr.Zero)
        {
            return;
        }

        switch (Classify(pageObject))
        {
            case Kind.HeaderOrFooter:
                found(pageObject, Bounds(pageObject, toPage));
                return;
            case Kind.Pagination:
                var bounds = Bounds(pageObject, toPage);
                if (bounds is { } onPage && InMarginBand(display.Map(onPage), height))
                {
                    found(pageObject, bounds);
                    return;
                }

                break;
        }

        // A form's objects are in its own coordinates, which its matrix maps onto those of
        // whatever draws it.
        if ((PdfPageObjectType) PdfiumNative.FPDFPageObj_GetType(pageObject) != PdfPageObjectType.Form ||
            depth >= maxFormDepth ||
            !PdfiumNative.FPDFPageObj_GetMatrix(pageObject, out var matrix))
        {
            return;
        }

        var formToPage = Concat(matrix, toPage);
        var count = PdfiumNative.FPDFFormObj_CountObjects(pageObject);
        for (var index = 0; index < count; index++)
        {
            Find(PdfiumNative.FPDFFormObj_GetObject(pageObject, (uint) index), formToPage, depth + 1, display, height, found);
        }
    }

    // Whether the displayed box lies wholly within the top or the bottom band.
    static bool InMarginBand(HtmlRect box, double height) =>
        box.Y + box.Height <= height * MarginBand ||
        box.Y >= height * (1 - MarginBand);

    // Adds a header or footer object's text objects to texts: the object itself, or for a
    // form, those within it.
    public static void AddTextObjects(IntPtr pageObject, HashSet<IntPtr> texts, int depth = 0)
    {
        switch ((PdfPageObjectType) PdfiumNative.FPDFPageObj_GetType(pageObject))
        {
            case PdfPageObjectType.Text:
                texts.Add(pageObject);
                break;
            case PdfPageObjectType.Form when depth < maxFormDepth:
                var count = PdfiumNative.FPDFFormObj_CountObjects(pageObject);
                for (var index = 0; index < count; index++)
                {
                    var child = PdfiumNative.FPDFFormObj_GetObject(pageObject, (uint) index);
                    if (child != IntPtr.Zero)
                    {
                        AddTextObjects(child, texts, depth + 1);
                    }
                }

                break;
        }
    }

    // The upright box around the object on the page.
    static PdfRectangle? Bounds(IntPtr pageObject, FsMatrix toPage)
    {
        if (!PdfiumNative.FPDFPageObj_GetBounds(pageObject, out var left, out var bottom, out var right, out var top))
        {
            return null;
        }

        var (x1, y1) = Map(toPage, left, bottom);
        var (x2, y2) = Map(toPage, right, bottom);
        var (x3, y3) = Map(toPage, left, top);
        var (x4, y4) = Map(toPage, right, top);
        return new(
            Math.Min(Math.Min(x1, x2), Math.Min(x3, x4)),
            Math.Min(Math.Min(y1, y2), Math.Min(y3, y4)),
            Math.Max(Math.Max(x1, x2), Math.Max(x3, x4)),
            Math.Max(Math.Max(y1, y2), Math.Max(y3, y4)));
    }

    static (double X, double Y) Map(FsMatrix matrix, double x, double y) =>
        (matrix.A * x + matrix.C * y + matrix.E, matrix.B * x + matrix.D * y + matrix.F);

    // The map that applies first and then second.
    static FsMatrix Concat(FsMatrix first, FsMatrix second) =>
        new()
        {
            A = first.A * second.A + first.B * second.C,
            B = first.A * second.B + first.B * second.D,
            C = first.C * second.A + first.D * second.C,
            D = first.C * second.B + first.D * second.D,
            E = first.E * second.A + first.F * second.C + second.E,
            F = first.E * second.B + first.F * second.D + second.F
        };

    enum Kind
    {
        Other,
        // A pagination artifact whose subtype is a name: a header or footer only where it lies.
        Pagination,
        HeaderOrFooter
    }

    // Stands for a property value that is a name, which PDFium cannot read.
    const string name = "\0name";

    // What the marked-content sequences the object sits in make it. PDFium reads a property's
    // value only when it is a string, while producers write /Type and /Subtype as names, of
    // which it tells only that they are names. Only pagination artifacts have a subtype, so
    // two names make a pagination artifact: a header or footer, or a watermark, page number or
    // Bates number, which Find tells apart by where it lies. Strings are read, and Pagination
    // with Header or Footer makes a header or footer wherever it lies.
    static Kind Classify(IntPtr pageObject)
    {
        var kind = Kind.Other;
        var count = PdfiumNative.FPDFPageObj_CountMarks(pageObject);
        for (var index = 0; index < count; index++)
        {
            var mark = PdfiumNative.FPDFPageObj_GetMark(pageObject, (uint) index);
            if (mark == IntPtr.Zero || !IsArtifact(mark))
            {
                continue;
            }

            var type = Value(mark, "Type\0"u8);
            if (type is not (name or "Pagination"))
            {
                continue;
            }

            var subtype = Value(mark, "Subtype\0"u8);
            if (subtype is "Header" or "Footer")
            {
                return Kind.HeaderOrFooter;
            }

            if (subtype == name)
            {
                kind = Kind.Pagination;
            }
        }

        return kind;
    }

    static bool IsArtifact(IntPtr mark)
    {
        // "Artifact" and its terminator in UTF-16LE.
        Span<byte> buffer = stackalloc byte[18];
        return PdfiumNative.FPDFPageObjMark_GetName(mark, buffer, (uint) buffer.Length, out var length) &&
               length == buffer.Length &&
               MemoryMarshal.Cast<byte, char>(buffer[..^2]).SequenceEqual("Artifact");
    }

    // The property's value: a string's text, name for a name, or null for anything else,
    // including a string too long to be one looked for.
    static string? Value(IntPtr mark, ReadOnlySpan<byte> key)
    {
        var type = PdfiumNative.FPDFPageObjMark_GetParamValueType(mark, key);
        if (type == PdfiumNative.ObjectName)
        {
            return name;
        }

        Span<byte> buffer = stackalloc byte[32];
        if (type != PdfiumNative.ObjectString ||
            !PdfiumNative.FPDFPageObjMark_GetParamStringValue(mark, key, buffer, (uint) buffer.Length, out var length) ||
            length < 2 ||
            length > buffer.Length)
        {
            return null;
        }

        return Encoding.Unicode.GetString(buffer[..((int) length - 2)]);
    }
}
