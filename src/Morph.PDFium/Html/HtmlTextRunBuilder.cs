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
    // The unit vector along the run's baseline.
    double directionX;
    double directionY;
    double fontSize;
    // How far the run reaches along its baseline from its origin.
    double extent;
    // The origin of the run's latest character.
    double lastX;
    double lastY;
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
    /// A drawn character: its code point, baseline origin, advance along the baseline, font
    /// size, the unit vector along its baseline and its style, all in page coordinates.
    /// </summary>
    public void Add(int codePoint, double x, double y, double advance, double size, double baselineX, double baselineY, HtmlTextStyle charStyle)
    {
        if (active && !Continues(x, y, size, baselineX, baselineY, charStyle))
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
            directionX = baselineX;
            directionY = baselineY;
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

        // How far along the baseline, from the run's origin, the character reaches.
        extent = Math.Max(extent, Along(x, y) + advance);
        lastX = x;
        lastY = y;
    }

    bool Continues(double x, double y, double size, double baselineX, double baselineY, HtmlTextStyle charStyle)
    {
        // A baseline turned more than about half a degree from the run's, or reversed, starts
        // another run.
        if (charStyle != style ||
            Math.Abs(size - fontSize) > fontSize * 0.01 ||
            Math.Abs(baselineX * directionY - baselineY * directionX) > 0.01 ||
            baselineX * directionX + baselineY * directionY < 0)
        {
            return false;
        }

        // The letters of a ligature, such as the f, f and i of "ﬃ", share one glyph and so one
        // origin. Measured against the glyph's whole width, the later letters would seem to
        // jump back.
        if (!pendingSpace && Math.Abs(x - lastX) < 0.01 && Math.Abs(y - lastY) < 0.01)
        {
            return true;
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
