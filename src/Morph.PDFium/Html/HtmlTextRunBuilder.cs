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
/// <para>
/// What counts as a word space is set by the run's own spaces, so a justified line, whose
/// spaces are all stretched alike, and a dot leader, whose dots are evenly spread, each stay
/// one run. A space well wider than the run's others ends the run there. So do wide spaces at
/// the start of a line that the rest of it does not match, such as the indent between a bullet
/// or a list number and its text, or the indents of a nested list item ("1. a) text"), which
/// would otherwise be spread over the whole line and leave its start under the bullet.
/// </para>
/// <para>
/// PDFium gives right-to-left text, Hebrew or Arabic, in reading order, so its characters
/// step leftwards along the baseline. A run whose second character, in a right-to-left
/// script, comes just before its first reads that way, and is written right to left.
/// </para>
/// </remarks>
sealed class HtmlTextRunBuilder(DisplayMatrix matrix)
{
    // A space is wide when it is more than this many ems, and more than this many times the
    // run's word space: wider than a double space, and than a monospaced font's space.
    const double wideSpace = 0.6;
    const double wideRatio = 2;

    // Splitting at an indent asks for a margin over those: a lone space more than this many
    // ems, since a monospaced space is exactly 0.6 em, and indents more than this many times
    // the line's word space, since a double space after a full stop is exactly twice it.
    const double loneIndent = 0.65;
    const double indentRatio = 2.2;

    readonly StringBuilder text = new();
    // The wide spaces at the start of the run, until its first ordinary space settles whether
    // they are indents: where the run would split at each, the length of its text and its end
    // before the space and the origin of the character after it, and the space's width.
    readonly List<(int Length, double End, double X, double Y, double Width)> indents = [];
    bool active;
    bool pendingSpace;
    double originX;
    double originY;
    // The unit vector along the run's baseline.
    double directionX;
    double directionY;
    // Whether the run's glyphs are drawn mirrored, by a character matrix that flips them.
    bool mirrored;
    double fontSize;
    // The stretch of its baseline the run covers, measured from its origin.
    double start;
    double end;
    // Whether the run reads backwards along its baseline: right-to-left text.
    bool reversed;
    int glyphs;
    int firstCodePoint;
    // The origin of the run's latest character.
    double lastX;
    double lastY;
    HtmlTextStyle style;
    // How many spaces the run holds, the width of its first (its word space, unless the first
    // spaces turn out to be indents), and whether its first ordinary space has settled that.
    int spaces;
    double wordSpace;
    bool settled;

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
    /// The second half of a surrogate pair, which PDFium reports as a character of its own
    /// with no meaningful position. It joins the current run.
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
    /// size, the unit vector along its baseline, whether its glyph is mirrored and its style,
    /// all in page coordinates.
    /// </summary>
    public void Add(int codePoint, double x, double y, double advance, double size, double baselineX, double baselineY, bool isMirrored, HtmlTextStyle charStyle)
    {
        if (active && !Continues(codePoint, x, y, advance, size, baselineX, baselineY, isMirrored, charStyle))
        {
            Flush();
        }

        if (active)
        {
            if (pendingSpace)
            {
                AddSpace(x, y, advance);
            }
        }
        else
        {
            active = true;
            originX = x;
            originY = y;
            directionX = baselineX;
            directionY = baselineY;
            mirrored = isMirrored;
            fontSize = size;
            style = charStyle;
            start = 0;
            end = 0;
            reversed = false;
            glyphs = 0;
            firstCodePoint = codePoint;
            spaces = 0;
            settled = false;
            indents.Clear();
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
            text.Append('\uFFFD');
        }

        var along = Along(x, y);
        start = Math.Min(start, along);
        end = Math.Max(end, along + advance);
        glyphs++;
        lastX = x;
        lastY = y;
    }

    // The width of the gap between the run and a character with this origin and advance that
    // carries it on: after its end, or before its start for a run that reads backwards.
    double Gap(double x, double y, double advance)
    {
        if (reversed)
        {
            return start - (Along(x, y) + advance);
        }

        return Along(x, y) - end;
    }

    // The pending space, before the character at (x, y), which continues the run.
    void AddSpace(double x, double y, double advance)
    {
        var width = Gap(x, y, advance);
        spaces++;
        if (spaces == 1)
        {
            wordSpace = width;
        }

        // Indents only start a line read forwards.
        if (!reversed && !settled)
        {
            if (width > fontSize * wideSpace)
            {
                indents.Add((text.Length, end, x, y, width));
            }
            else
            {
                // The run's first ordinary space: the wide ones before it were indents, between
                // a list number and its text say, when it is well narrower than all of them.
                if (indents.Count > 0 && AllWiderThan(width * indentRatio))
                {
                    SplitAtIndents();
                    wordSpace = width;
                }

                settled = true;
                indents.Clear();
            }
        }

        text.Append(' ');
    }

    bool AllWiderThan(double width)
    {
        foreach (var indent in indents)
        {
            if (indent.Width <= width)
            {
                return false;
            }
        }

        return true;
    }

    // Whether space is wide next to wordSpace.
    bool IsWide(double space, double word) =>
        space > fontSize * wideSpace && space > word * wideRatio;

    bool Continues(int codePoint, double x, double y, double advance, double size, double baselineX, double baselineY, bool isMirrored, HtmlTextStyle charStyle)
    {
        // A baseline turned more than about half a degree from the run's, or reversed, starts
        // another run.
        if (charStyle != style ||
            isMirrored != mirrored ||
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

        if (Fits(Gap(x, y, advance)))
        {
            return true;
        }

        // The run's second character, in a right-to-left script, coming just before its first
        // rather than after it: the run reads backwards.
        if (glyphs == 1 &&
            !reversed &&
            (IsRightToLeft(codePoint) || IsRightToLeft(firstCodePoint)) &&
            Fits(start - (Along(x, y) + advance)))
        {
            reversed = true;
            return true;
        }

        return false;
    }

    // Whether a gap this wide carries the run on.
    bool Fits(double gap)
    {
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

    // Hebrew, Arabic, Syriac, Thaana, N'Ko, Samaritan and Mandaic, and their presentation
    // forms: the scripts written right to left that PDFium gives in reading order.
    static bool IsRightToLeft(int codePoint) =>
        codePoint is >= 0x0590 and <= 0x08FF or >= 0xFB1D and <= 0xFDFF or >= 0xFE70 and <= 0xFEFF;

    double Along(double x, double y) =>
        (x - originX) * directionX + (y - originY) * directionY;

    // Ends a run at each of its indents in turn, carrying on from the character after the last.
    void SplitAtIndents()
    {
        var removed = 0;
        var shift = 0d;
        foreach (var (length, indentEnd, x, y, _) in indents)
        {
            var part = length - removed;
            Emit(text.ToString(0, part), originX, originY, indentEnd - shift);
            var offset = Along(x, y);
            // Past the space itself.
            text.Remove(0, part + 1);
            removed += part + 1;
            shift += offset;
            end -= offset;
            originX = x;
            originY = y;
        }
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

        // A run whose one space is wide is a bullet or a list number and a few words.
        if (!reversed && !settled && spaces == 1 && indents.Count == 1 && wordSpace > fontSize * loneIndent)
        {
            SplitAtIndents();
        }

        if (reversed)
        {
            // Read from its right-hand end, where its first character is.
            Emit(text.ToString(), originX + end * directionX, originY + end * directionY, end - start);
        }
        else
        {
            Emit(text.ToString(), originX, originY, end);
        }

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
        Runs.Add(new(value, displayX, displayY, degrees, fontSize, length, style, reversed, mirrored));
    }
}
