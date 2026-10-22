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
/// <remarks>
/// What counts as a word space is set by the run's own spaces, so a justified line, whose
/// spaces are all stretched alike, and a dot leader, whose dots are evenly spread, each stay
/// one run. A space well wider than the run's others ends the run there. So does a wide first
/// space that the rest of the line does not match, such as the indent between a bullet or a
/// list number and its text, which would otherwise be spread over the whole line and leave
/// its start under the bullet.
/// </remarks>
sealed class HtmlTextRunBuilder(DisplayMatrix matrix)
{
    // A space is wide when it is more than this many ems, and more than twice the run's word
    // space: wider than a double space, and than a monospaced font's space.
    const double wideSpace = 0.6;

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
    // How many spaces the run holds, and the width of its first: its word space.
    int spaces;
    double wordSpace;
    // Where the run would split at its first space, should that turn out to be wide: the
    // length of its text and its extent before the space, and the origin of the character
    // after it.
    int splitLength;
    double splitExtent;
    double splitX;
    double splitY;

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
                AddSpace(x, y);
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
            spaces = 0;
        }

        pendingSpace = false;
        if (codePoint <= char.MaxValue)
        {
            // Including a first surrogate half, which AppendLowSurrogate completes.
            text.Append((char) codePoint);
        }
        else if (Rune.IsValid(codePoint))
        {
            Span<char> pair = stackalloc char[2];
            text.Append(pair[..new Rune(codePoint).EncodeToUtf16(pair)]);
        }
        else
        {
            text.Append('�');
        }

        // How far along the baseline, from the run's origin, the character reaches.
        extent = Math.Max(extent, Along(x, y) + advance);
        lastX = x;
        lastY = y;
    }

    // The pending space, before the character at (x, y), which continues the run.
    void AddSpace(double x, double y)
    {
        var gap = Along(x, y) - extent;
        spaces++;
        if (spaces == 1)
        {
            wordSpace = gap;
            splitLength = text.Length;
            splitExtent = extent;
            splitX = x;
            splitY = y;
        }
        else if (spaces == 2 && IsWide(wordSpace, gap))
        {
            // The first space was wide and this one is not, so the first was an indent rather
            // than the line's word space. What came before it is a run of its own.
            SplitAtFirstSpace();
            spaces = 1;
            wordSpace = gap;
            splitLength = text.Length;
            splitExtent = extent;
            splitX = x;
            splitY = y;
        }

        text.Append(' ');
    }

    // Whether space is wide next to wordSpace.
    bool IsWide(double space, double word) =>
        space > fontSize * wideSpace && space > word * 2;

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
        if (!pendingSpace)
        {
            return gap >= -fontSize * 0.5 && gap <= fontSize * 0.8;
        }

        if (gap < -fontSize * 0.5 || gap > fontSize * 1.5)
        {
            return false;
        }

        // A space well wider than the run's word space: a gap between columns, say.
        return spaces == 0 || !IsWide(gap, wordSpace);
    }

    double Along(double x, double y) =>
        (x - originX) * directionX + (y - originY) * directionY;

    // Ends a run at its first space, carrying on from the character after it.
    void SplitAtFirstSpace()
    {
        Emit(text.ToString(0, splitLength), originX, originY, splitExtent);
        var offset = Along(splitX, splitY);
        // Past the space itself.
        text.Remove(0, splitLength + 1);
        extent -= offset;
        originX = splitX;
        originY = splitY;
    }

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

        // A run with one space, a wide one, is a bullet or a list number and a few words.
        if (spaces == 1 && wordSpace > fontSize * wideSpace)
        {
            SplitAtFirstSpace();
        }

        Emit(text.ToString(), originX, originY, extent);
        text.Clear();
        active = false;
        pendingSpace = false;
    }

    void Emit(string value, double x, double y, double length)
    {
        var (displayX, displayY) = matrix.Map(x, y);
        var (dx, dy) = matrix.MapVector(directionX, directionY);
        // Display y runs down, so this is the clockwise angle SVG's rotate expects.
        var degrees = Math.Atan2(dy, dx) * 180 / Math.PI;
        Runs.Add(new(value, displayX, displayY, degrees, fontSize, length, style));
    }
}
