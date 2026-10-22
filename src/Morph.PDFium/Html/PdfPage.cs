using System.Globalization;

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
    public string RenderHtml(HtmlOptions? options = null) =>
        RenderHtmlPage(options, false);

    // owned is true for the library's own pages, loaded to convert and disposed straight after,
    // whose text can be hidden in place for the image (see RenderWithoutText).
    internal string RenderHtmlPage(HtmlOptions? options, bool owned)
    {
        options ??= new();
        options.Validate(nameof(options));
        var layout = ReadHtmlLayout(options);
        string? imageUrl = null;
        byte[]? png = null;
        if (options.IncludeImage && options.ImageUrlFromPixels is { } fromPixels)
        {
            using var rendered = RenderHtmlPixels(options, owned);
            imageUrl = fromPixels(layout.Index, new(rendered.Pixels, rendered.Width, rendered.Height, options.Render.Dpi));
        }
        else if (options.IncludeImage)
        {
            // Visible text is drawn over the image, so the image leaves it out rather than show
            // it twice.
            png = options.Text == HtmlTextMode.Visible ? RenderWithoutText(options.Render, owned) : Render(options.Render);
            if (options.ImageUrl is not null)
            {
                imageUrl = options.ImageUrl(layout.Index, png);
                png = null;
            }
        }

        return HtmlWriter.Page(layout, imageUrl, png, options);
    }

    // The page's image as pixels, for ImageUrlFromPixels: without its text under visible text,
    // as RenderHtmlPage's PNG is.
    RenderedPixels RenderHtmlPixels(HtmlOptions options, bool owned)
    {
        var render = options.Render;
        if (options.Text == HtmlTextMode.Visible)
        {
            return RenderPixelsWithoutText(render, owned);
        }

        return RenderPixels(render.Dpi, render.ToFlags(), render.ToFillColor(), null);
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
            links = ReadHtmlLinks(matrix, options);
        }

        return new(Index, width, height, runs, links);
    }

    // Caller must hold PdfiumNative.Sync. Works out the map from page coordinates to display
    // coordinates by asking FPDF_PageToDevice where three points land, which accounts for the
    // page's rotation and for a media or crop box away from the origin. Three corners of the
    // page box are used, which land within the device whatever the box's offset; a malformed
    // page (a crop box outside its media box) can leave the box empty, and then any three
    // points not in a line do.
    DisplayMatrix ReadDisplayMatrix(IntPtr page, double width, double height)
    {
        // FPDF_PageToDevice answers in whole device units, so ask about a device many times the
        // page's size in points to keep precision below a point: a thousand times, or as many
        // as fit in its int coordinates for a page far larger than any paper.
        var unitsPerPoint = (int) Math.Clamp(int.MaxValue / 4d / Math.Max(1, Math.Max(width, height)), 1, 1000);
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

        var (left, bottom, spanX, spanY) = (0d, 0d, 1000d, 1000d);
        if (PdfiumNative.FPDF_GetPageBoundingBox(page, out var box) &&
            box.Right > box.Left &&
            box.Top > box.Bottom)
        {
            (left, bottom, spanX, spanY) = (box.Left, box.Bottom, (double) box.Right - box.Left, (double) box.Top - box.Bottom);
        }

        var origin = ToDisplay(left, bottom);
        var right = ToDisplay(left + spanX, bottom);
        var top = ToDisplay(left, bottom + spanY);
        // A quarter turn's coefficients are exactly 0 or ±1; snap off the device rounding.
        var a = Snap((right.X - origin.X) / spanX);
        var b = Snap((right.Y - origin.Y) / spanX);
        var c = Snap((top.X - origin.X) / spanY);
        var d = Snap((top.Y - origin.Y) / spanY);
        return new(a, b, c, d, origin.X - a * left - c * bottom, origin.Y - b * left - d * bottom);
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
        // A page's many text objects share a few fonts, so the font's part of the look is
        // worked out once per font.
        var fonts = new Dictionary<IntPtr, HtmlTextStyle>();
        // Whether the previous character was the first half of a surrogate pair that went into
        // a run, so the second half belongs with it.
        var highSurrogateAdded = false;
        for (var index = 0; index < count; index++)
        {
            var unicode = PdfiumNative.FPDFText_GetUnicode(text, index);
            var afterHighSurrogate = highSurrogateAdded;
            highSurrogateAdded = false;
            if (unicode is '\r' or '\n')
            {
                builder.Flush();
                continue;
            }

            if (unicode is ' ' or '\t' or '\u00A0' || PdfiumNative.FPDFText_IsGenerated(text, index) == 1)
            {
                builder.Space();
                continue;
            }

            // Unmapped characters (0) and other control characters have nothing to show.
            if (unicode < 0x20)
            {
                continue;
            }

            // A second half whose first half was left out (drawn invisibly, say) goes too.
            if (unicode is >= 0xDC00 and <= 0xDFFF)
            {
                if (afterHighSurrogate)
                {
                    builder.AppendLowSurrogate((char) unicode);
                }

                continue;
            }

            HtmlTextStyle style = default;
            if (visible)
            {
                var textObject = PdfiumNative.FPDFText_GetTextObject(text, index);
                if (!styles.TryGetValue(textObject, out var cached))
                {
                    cached = ReadTextStyle(text, index, textObject, fonts);
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
            // PDFium reports the size the text was set at. The text matrix and the page's
            // transformation then scale it to the size it is drawn at: some producers set all
            // text at size 1 and scale it up that way.
            var fontSize = PdfiumNative.FPDFText_GetFontSize(text, index);
            // The character's matrix: its text matrix combined with the page's transformation.
            double a, b, c, d;
            if (PdfiumNative.FPDFText_GetMatrix(text, index, out var charMatrix) &&
                (charMatrix.A != 0 || charMatrix.B != 0))
            {
                (a, b, c, d) = (charMatrix.A, charMatrix.B, charMatrix.C, charMatrix.D);
            }
            else
            {
                // Without one, a plain turn by PDFium's character angle, which it measures from
                // the matrix's y axis, so it is off for sheared text. A failed read returns -1.
                // The angle is clockwise, though page y runs up.
                var (sin, cos) = Math.SinCos(Math.Max(0, PdfiumNative.FPDFText_GetCharAngle(text, index)));
                (a, b, c, d) = (cos, -sin, sin, cos);
            }

            // The baseline runs along the matrix's x axis.
            var length = Math.Sqrt(a * a + b * b);
            // The size the text was set at, scaled by how far the matrix stretches the glyph
            // across its baseline: the area of the unit square it maps to over that square's
            // width along the baseline. Unlike the length of its y axis, this ignores the slant
            // of sheared text. Some producers set all text at size 1 and scale it up this way.
            fontSize *= Math.Abs(a * d - b * c) / length;
            if (fontSize <= 0)
            {
                fontSize = Math.Max(1, box.Height);
            }

            // A combining mark or a zero-width character takes no room of its own. PDFium's loose
            // box cannot say so: for a glyph with no advance it falls back to the inked part.
            var advance = TakesNoAdvance((int) unicode) ? 0 : Advance(box, a, b, c, d, fontSize);
            // A negative determinant flips the glyphs: mirrored text, or text drawn upside down
            // and back to front.
            builder.Add((int) unicode, x, y, advance, fontSize, a / length, b / length, a * d - b * c < 0, style);
            highSurrogateAdded = unicode is >= 0xD800 and <= 0xDBFF;
        }

        builder.Flush();
        return builder.Runs;
    }

    // Combining marks (an accent over the letter before it, a Thai or Indic vowel sign) and
    // zero-width format characters (ZWSP, ZWJ). A first surrogate half, standing for a code
    // point it cannot classify alone, is taken to advance.
    static bool TakesNoAdvance(int codePoint) =>
        codePoint is < 0xD800 or > 0xDFFF &&
        CharUnicodeInfo.GetUnicodeCategory(codePoint) is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format;

    // The character's advance along its baseline, in page points, from its loose box. That box
    // is the upright rectangle around the character's text space box (its advance w across,
    // the font's ascent to descent h up) mapped by its matrix (a b c d), so it is |a|w + |c|h
    // wide and |b|w + |d|h high. Solving those for w and scaling it by the length of the
    // matrix's x axis gives the advance. Where the two cannot be told apart, for text turned
    // near 45 degrees, or there is no box, half an em is assumed. A box with no width but some
    // height is a mark with no advance of its own, such as a combining accent.
    static double Advance(PdfRectangle box, double a, double b, double c, double d, double fontSize)
    {
        if (box.Width <= 0 && box.Height <= 0)
        {
            return fontSize * 0.5;
        }

        var (absA, absB, absC, absD) = (Math.Abs(a), Math.Abs(b), Math.Abs(c), Math.Abs(d));
        var xAxis = Math.Sqrt(a * a + b * b);
        var yAxis = Math.Sqrt(c * c + d * d);
        var determinant = absA * absD - absC * absB;
        if (Math.Abs(determinant) < 0.25 * xAxis * yAxis)
        {
            return fontSize * 0.5;
        }

        var width = (box.Width * absD - box.Height * absC) / determinant;
        // However the box is turned, no side of it is longer than its width and height
        // together, which bounds a solution thrown off by rounding.
        return Math.Clamp(width * xAxis, 0, box.Width + box.Height);
    }

    // Caller must hold PdfiumNative.Sync. The look of the character at index, or null when its
    // text object draws it invisibly. The font's part of it is taken from fonts, or worked out
    // and added there.
    static HtmlTextStyle? ReadTextStyle(IntPtr text, int index, IntPtr textObject, Dictionary<IntPtr, HtmlTextStyle> fonts)
    {
        var mode = PdfiumNative.TextRenderModeFill;
        if (textObject != IntPtr.Zero)
        {
            // Modes 4 to 7 draw as 0 to 3 do; an unknown mode (-1) is drawn filled.
            mode = Math.Max(PdfiumNative.TextRenderModeFill, PdfiumNative.FPDFTextObj_GetTextRenderMode(textObject)) % 4;
        }

        if (mode == PdfiumNative.TextRenderModeInvisible)
        {
            return null;
        }

        var font = IntPtr.Zero;
        if (textObject != IntPtr.Zero)
        {
            font = PdfiumNative.FPDFTextObj_GetFont(textObject);
        }

        if (font == IntPtr.Zero || !fonts.TryGetValue(font, out var look))
        {
            var flags = 0;
            var fontName = Interop.Utf8ByLength((text, index), static (state, buffer, length) => PdfiumNative.FPDFText_GetFontInfo(state.text, state.index, buffer, length, out _));
            if (fontName is not null)
            {
                PdfiumNative.FPDFText_GetFontInfo(text, index, [], 0, out flags);
            }

            look = HtmlFonts.Style(fontName, flags, PdfiumNative.FPDFText_GetFontWeight(text, index));
            if (font != IntPtr.Zero)
            {
                fonts[font] = look;
            }
        }

        PdfColor? fill = null;
        if (mode is PdfiumNative.TextRenderModeFill or PdfiumNative.TextRenderModeFillStroke)
        {
            fill = new PdfColor(0, 0, 0, 255);
            if (PdfiumNative.FPDFText_GetFillColor(text, index, out var r, out var g, out var b, out var a))
            {
                fill = new((byte) r, (byte) g, (byte) b, (byte) a);
            }
        }

        PdfColor? stroke = null;
        double strokeWidth = 0;
        if (mode is PdfiumNative.TextRenderModeStroke or PdfiumNative.TextRenderModeFillStroke)
        {
            stroke = new PdfColor(0, 0, 0, 255);
            if (PdfiumNative.FPDFText_GetStrokeColor(text, index, out var r, out var g, out var b, out var a))
            {
                stroke = new((byte) r, (byte) g, (byte) b, (byte) a);
            }

            if (textObject != IntPtr.Zero && PdfiumNative.FPDFPageObj_GetStrokeWidth(textObject, out var width))
            {
                strokeWidth = width;
            }
        }

        return look with
        {
            Fill = fill,
            Stroke = stroke,
            StrokeWidth = strokeWidth
        };
    }

    List<HtmlLink> ReadHtmlLinks(DisplayMatrix matrix, HtmlOptions options)
    {
        var links = new List<HtmlLink>();
        var annotated = new List<(string Href, PdfRectangle Rectangle)>();
        foreach (var link in GetLinks())
        {
            var href = HtmlWriter.Href(link.Destination, link.Action, options);
            if (href is null || link.Rectangle.Width <= 0 || link.Rectangle.Height <= 0)
            {
                continue;
            }

            annotated.Add((href, link.Rectangle));
            links.Add(new(href, [matrix.Map(link.Rectangle)]));
        }

        // PDFium finds URLs in the text whether or not a link annotation already covers them.
        // One is left out only where an annotation to the same URL lies over it; the same URL
        // linked elsewhere on the page, in a footer say, leaves the text's link in place.
        foreach (var webLink in GetWebLinks())
        {
            var href = HtmlWriter.SafeUrl(webLink.Url, options);
            if (href is null || IsAnnotated(href, webLink.Rectangles, annotated))
            {
                continue;
            }

            links.Add(new(href, [.. webLink.Rectangles.Select(_ => matrix.Map(_))]));
        }

        return links;
    }

    static bool IsAnnotated(string href, IReadOnlyList<PdfRectangle> rectangles, List<(string Href, PdfRectangle Rectangle)> annotated)
    {
        foreach (var (annotatedHref, annotation) in annotated)
        {
            if (annotatedHref != href)
            {
                continue;
            }

            foreach (var rectangle in rectangles)
            {
                if (rectangle.Left < annotation.Right &&
                    annotation.Left < rectangle.Right &&
                    rectangle.Bottom < annotation.Top &&
                    annotation.Bottom < rectangle.Top)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
