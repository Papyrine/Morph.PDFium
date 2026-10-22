using Morph.PDFium;

/// <summary>
/// Groups a page's characters, fed in text stream order, into <see cref="HtmlTextRun"/>s:
/// each run holds the characters that follow on from one another along one baseline in one
/// size and style. A run ends where the text moves to another line, jumps back, leaves a gap
/// wider than a word space (a table column, say) or changes size, angle or style, since the
/// browser spreads the run's glyphs evenly over its length and a run spanning any of those
/// would put them in the wrong place. A run never starts or ends with a space: the browser
/// copies separate runs onto separate lines, so none is needed between them, and one would
/// take up part of the run's length, which only the run's glyphs should fill.
/// </summary>
sealed class HtmlTextRunBuilder(DisplayMatrix matrix)
{
    readonly StringBuilder text = new();
    bool active;
    bool pendingSpace;
    double originX;
    double originY;
    double directionX;
    double directionY;
    double angle;
    double fontSize;
    // How far the run reaches along its baseline from its origin.
    double extent;
    HtmlTextStyle style;

    public List<HtmlTextRun> Runs { get; } = [];

    /// <summary>A space between words: kept only when more of the run follows it.</summary>
    public void Space()
    {
        if (active)
        {
            pendingSpace = true;
        }
    }

    /// <summary>
    /// The second half of a surrogate pair, which PDFium on Windows reports as a character of
    /// its own with no meaningful position. It joins the current run.
    /// </summary>
    public void AppendLowSurrogate(char value)
    {
        if (active)
        {
            text.Append(value);
        }
    }

    /// <summary>
    /// A drawn character: its code point, baseline origin, bounding box (zero when unknown),
    /// font size, angle in radians and style, all in page coordinates.
    /// </summary>
    public void Add(int codePoint, double x, double y, PdfRectangle box, double size, double radians, HtmlTextStyle charStyle)
    {
        if (active && !Continues(x, y, size, radians, charStyle))
        {
            Flush();
        }

        if (active)
        {
            if (pendingSpace)
            {
                text.Append(' ');
            }
        }
        else
        {
            active = true;
            originX = x;
            originY = y;
            angle = radians;
            // PDFium measures the angle clockwise, though page y runs up.
            directionX = Math.Cos(radians);
            directionY = -Math.Sin(radians);
            fontSize = size;
            style = charStyle;
            extent = 0;
        }

        pendingSpace = false;
        if (Rune.IsValid(codePoint))
        {
            text.Append(new Rune(codePoint).ToString());
        }
        else
        {
            // A lone surrogate half, kept for AppendLowSurrogate to complete.
            text.Append((char) codePoint);
        }

        extent = Math.Max(extent, Reach(x, y, box, size));
    }

    bool Continues(double x, double y, double size, double radians, HtmlTextStyle charStyle)
    {
        if (charStyle != style ||
            Math.Abs(size - fontSize) > fontSize * 0.01 ||
            Math.Abs(radians - angle) > 0.01)
        {
            return false;
        }

        var dx = x - originX;
        var dy = y - originY;
        // Off the baseline: another line, or a superscript drawn at the same size.
        var across = dy * directionX - dx * directionY;
        if (Math.Abs(across) > fontSize * 0.25)
        {
            return false;
        }

        var gap = dx * directionX + dy * directionY - extent;
        var widest = pendingSpace ? 1.5 : 0.8;
        return gap >= -fontSize * 0.5 && gap <= fontSize * widest;
    }

    // How far along the baseline, from the run's origin, the character reaches: the furthest
    // corner of its box, or half an em past its origin when it has no box.
    double Reach(double x, double y, PdfRectangle box, double size)
    {
        if (box.Width <= 0 || box.Height <= 0)
        {
            return Along(x, y) + size * 0.5;
        }

        return Math.Max(
            Math.Max(Along(box.Left, box.Bottom), Along(box.Right, box.Bottom)),
            Math.Max(Along(box.Left, box.Top), Along(box.Right, box.Top)));
    }

    double Along(double x, double y) =>
        (x - originX) * directionX + (y - originY) * directionY;

    /// <summary>
    /// Ends the current run, if any: at the end of a line, and once more after the last
    /// character.
    /// </summary>
    public void Flush()
    {
        if (!active)
        {
            return;
        }

        var (x, y) = matrix.Map(originX, originY);
        var (dx, dy) = matrix.MapVector(directionX, directionY);
        // Display y runs down, so this is the clockwise angle SVG's rotate expects.
        var degrees = Math.Atan2(dy, dx) * 180 / Math.PI;
        Runs.Add(new(text.ToString(), x, y, degrees, fontSize, extent, style));

        text.Clear();
        active = false;
        pendingSpace = false;
    }
}
