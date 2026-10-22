namespace Morph.PDFium;

public sealed partial class PdfPage
{
    /// <summary>
    /// Renders this page to an HTML fragment: a <c>div</c> the size of the page, with the id
    /// <see cref="HtmlOptions.PageIdPrefix"/> plus the one-based page number, holding the
    /// page's image and, in an inline SVG over it, its text and links. The fragment carries
    /// the styles it needs inline, so it can be placed in any HTML page; it shrinks to fit a
    /// narrower container, keeping its proportions. Use
    /// <see cref="PdfiumDocument.RenderHtml(HtmlOptions?)"/> for a complete HTML document.
    /// </summary>
    public string RenderHtml(HtmlOptions? options = null)
    {
        options ??= new();
        options.Validate(nameof(options));
        var layout = ReadHtmlLayout(options);
        string? image = null;
        if (options.IncludeImage)
        {
            var png = Render(options.Render);
            image = options.ImageUrl?.Invoke(layout.Index, png) ?? $"data:image/png;base64,{Convert.ToBase64String(png)}";
        }

        return HtmlWriter.Page(layout, image, options);
    }

    HtmlPageLayout ReadHtmlLayout(HtmlOptions options)
    {
        double width;
        double height;
        DisplayMatrix matrix;
        IReadOnlyList<HtmlTextRun> runs = [];
        lock (PdfiumNative.Sync)
        {
            var page = ValidHandle();
            width = PdfiumNative.FPDF_GetPageWidthF(page);
            height = PdfiumNative.FPDF_GetPageHeightF(page);
            matrix = ReadDisplayMatrix(page, width, height);
            if (options.Text != HtmlTextMode.None)
            {
                runs = ReadTextRuns(matrix, options.Text == HtmlTextMode.Visible);
            }
        }

        IReadOnlyList<HtmlLink> links = [];
        if (options.IncludeLinks)
        {
            links = ReadHtmlLinks(matrix, options.PageIdPrefix);
        }

        return new(Index, width, height, runs, links);
    }

    // Caller must hold PdfiumNative.Sync. Works out the map from page coordinates to display
    // coordinates by asking FPDF_PageToDevice where three corners of the page box land, which
    // accounts for the page's rotation and for a media or crop box away from the origin.
    DisplayMatrix ReadDisplayMatrix(IntPtr page, double width, double height)
    {
        if (!PdfiumNative.FPDF_GetPageBoundingBox(page, out var box) ||
            box.Right <= box.Left ||
            box.Top <= box.Bottom)
        {
            throw new PdfiumException($"Failed to read bounding box of page {Index}");
        }

        // FPDF_PageToDevice answers in whole device units, so ask about a device this many
        // times the page's size in points to keep precision below a point.
        const int unitsPerPoint = 1000;
        var deviceWidth = (int) Math.Round(width * unitsPerPoint);
        var deviceHeight = (int) Math.Round(height * unitsPerPoint);
        (double X, double Y) ToDisplay(double x, double y)
        {
            if (!PdfiumNative.FPDF_PageToDevice(page, 0, 0, deviceWidth, deviceHeight, 0, x, y, out var deviceX, out var deviceY))
            {
                throw new PdfiumException($"Failed to map page coordinates onto page {Index}");
            }

            return ((double) deviceX / unitsPerPoint, (double) deviceY / unitsPerPoint);
        }

        var origin = ToDisplay(box.Left, box.Bottom);
        var right = ToDisplay(box.Right, box.Bottom);
        var top = ToDisplay(box.Left, box.Top);
        var boxWidth = (double) box.Right - box.Left;
        var boxHeight = (double) box.Top - box.Bottom;
        // A quarter turn's coefficients are exactly 0 or ±1; snap off the device rounding.
        var a = Snap((right.X - origin.X) / boxWidth);
        var b = Snap((right.Y - origin.Y) / boxWidth);
        var c = Snap((top.X - origin.X) / boxHeight);
        var d = Snap((top.Y - origin.Y) / boxHeight);
        return new(a, b, c, d, origin.X - a * box.Left - c * box.Bottom, origin.Y - b * box.Left - d * box.Bottom);
    }

    static double Snap(double value)
    {
        var rounded = Math.Round(value);
        if (Math.Abs(value - rounded) < 0.001)
        {
            return rounded;
        }

        return value;
    }

    // Caller must hold PdfiumNative.Sync.
    List<HtmlTextRun> ReadTextRuns(DisplayMatrix matrix, bool visible)
    {
        var text = TextHandle();
        var count = PdfiumNative.FPDFText_CountChars(text);
        var builder = new HtmlTextRunBuilder(matrix);
        // Characters come in long stretches from one text object, which fixes their font and
        // color, so the style is worked out once per object. Null marks invisible text.
        var styles = new Dictionary<IntPtr, HtmlTextStyle?>();
        for (var index = 0; index < count; index++)
        {
            var unicode = PdfiumNative.FPDFText_GetUnicode(text, index);
            if (unicode is '\r' or '\n')
            {
                builder.Flush();
                continue;
            }

            if (unicode is ' ' or '\t' or ' ' || PdfiumNative.FPDFText_IsGenerated(text, index) == 1)
            {
                builder.Space();
                continue;
            }

            // Unmapped characters (0) and other control characters have nothing to show.
            if (unicode < 0x20)
            {
                continue;
            }

            if (unicode is >= 0xDC00 and <= 0xDFFF)
            {
                builder.AppendLowSurrogate((char) unicode);
                continue;
            }

            HtmlTextStyle style = default;
            if (visible)
            {
                var textObject = PdfiumNative.FPDFText_GetTextObject(text, index);
                if (!styles.TryGetValue(textObject, out var cached))
                {
                    cached = ReadTextStyle(text, index, textObject);
                    styles[textObject] = cached;
                }

                if (cached is not { } found)
                {
                    continue;
                }

                style = found;
            }

            if (!PdfiumNative.FPDFText_GetCharOrigin(text, index, out var x, out var y))
            {
                continue;
            }

            // The loose box spans the glyph's advance and the font's ascent and descent, which
            // is where the next character starts, rather than only the inked part of the glyph.
            var box = PdfiumNative.FPDFText_GetLooseCharBox(text, index, out var loose)
                ? new PdfRectangle(loose.Left, loose.Bottom, loose.Right, loose.Top)
                : default;
            var fontSize = PdfiumNative.FPDFText_GetFontSize(text, index);
            if (fontSize <= 0)
            {
                fontSize = Math.Max(1, box.Height);
            }

            var angle = PdfiumNative.FPDFText_GetCharAngle(text, index);
            builder.Add((int) unicode, x, y, box, fontSize, Math.Max(0, angle), style);
        }

        builder.Flush();
        return builder.Runs;
    }

    // Caller must hold PdfiumNative.Sync. The look of the character at index, or null when its
    // text object draws it invisibly.
    static HtmlTextStyle? ReadTextStyle(IntPtr text, int index, IntPtr textObject)
    {
        if (textObject != IntPtr.Zero &&
            PdfiumNative.FPDFTextObj_GetTextRenderMode(textObject) == PdfiumNative.TextRenderModeInvisible)
        {
            return null;
        }

        var flags = 0;
        var fontName = Interop.Utf8ByLength((text, index), static (state, buffer, length) => PdfiumNative.FPDFText_GetFontInfo(state.text, state.index, buffer, length, out _));
        if (fontName is not null)
        {
            PdfiumNative.FPDFText_GetFontInfo(text, index, [], 0, out flags);
        }

        var color = new PdfColor(0, 0, 0, 255);
        if (PdfiumNative.FPDFText_GetFillColor(text, index, out var r, out var g, out var b, out var a))
        {
            color = new((byte) r, (byte) g, (byte) b, (byte) a);
        }

        return HtmlFonts.Style(fontName, flags, PdfiumNative.FPDFText_GetFontWeight(text, index), color);
    }

    List<HtmlLink> ReadHtmlLinks(DisplayMatrix matrix, string pageIdPrefix)
    {
        var links = new List<HtmlLink>();
        var hrefs = new HashSet<string>();
        foreach (var link in GetLinks())
        {
            var href = HtmlWriter.Href(link.Destination, link.Action, pageIdPrefix);
            if (href is null || link.Rectangle.Width <= 0 || link.Rectangle.Height <= 0)
            {
                continue;
            }

            hrefs.Add(href);
            links.Add(new(href, [matrix.Map(link.Rectangle)]));
        }

        // PDFium finds URLs in the text whether or not a link annotation already covers them.
        foreach (var webLink in GetWebLinks())
        {
            var href = HtmlWriter.SafeUrl(webLink.Url);
            if (href is null || !hrefs.Add(href))
            {
                continue;
            }

            links.Add(new(href, [.. webLink.Rectangles.Select(_ => matrix.Map(_))]));
        }

        return links;
    }
}
