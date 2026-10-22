using Morph.PDFium;

/// <summary>
/// Works out how visible HTML text should look from what PDFium reports about a character's
/// font: its PostScript name (such as "ABCDEF+Arial-BoldItalicMT"), its font descriptor flags
/// and its weight.
/// </summary>
static class HtmlFonts
{
    // Font descriptor flags (PDF 32000-1:2008, 9.8.2).
    const int fixedPitch = 1;
    const int serif = 2;
    const int italic = 64;
    const int forceBold = 1 << 18;

    public static HtmlTextStyle Style(string? fontName, int flags, int weight, PdfColor fill)
    {
        var name = StripSubsetPrefix(fontName);
        var family = Family(name);
        var bold = (flags & forceBold) != 0 || Contains(name, "Bold") || Contains(name, "Black") || Contains(name, "Heavy");
        if (weight <= 0)
        {
            weight = 400;
        }

        // Many fonts leave the weight at its default, whatever their name says.
        if (bold && weight <= 400)
        {
            weight = 700;
        }

        var isItalic = (flags & italic) != 0 || Contains(name, "Italic") || Contains(name, "Oblique");
        return new(family, Generic(name, flags), weight, isItalic, fill);
    }

    // A subset font's name starts with six capital letters and a plus sign.
    static string? StripSubsetPrefix(string? name)
    {
        if (name is { Length: > 7 } && name[6] == '+' && !name.AsSpan(0, 6).ContainsAnyExceptInRange('A', 'Z'))
        {
            return name[7..];
        }

        return name;
    }

    // The family is the name before its style: "Arial" from "Arial-BoldMT" or "Arial,Bold".
    // Only letters, digits, spaces and a few separators are kept, so the result can go
    // straight into a quoted CSS string.
    static string? Family(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        var end = name.IndexOfAny(['-', ',']);
        if (end > 0)
        {
            name = name[..end];
        }

        var builder = new StringBuilder(name.Length);
        foreach (var character in name)
        {
            if (char.IsAsciiLetterOrDigit(character) || character is ' ' or '_' or '.')
            {
                builder.Append(character);
            }
        }

        if (builder.Length == 0)
        {
            return null;
        }

        return builder.ToString();
    }

    static string Generic(string? name, int flags)
    {
        if ((flags & fixedPitch) != 0 || Contains(name, "Courier") || Contains(name, "Mono") || Contains(name, "Consol"))
        {
            return "monospace";
        }

        if (Contains(name, "Sans") || Contains(name, "Arial") || Contains(name, "Helvetica"))
        {
            return "sans-serif";
        }

        if ((flags & serif) != 0 || Contains(name, "Times") || Contains(name, "Serif") || Contains(name, "Georgia") || Contains(name, "Garamond"))
        {
            return "serif";
        }

        return "sans-serif";
    }

    static bool Contains(string? name, string value) =>
        name is not null && name.Contains(value, StringComparison.OrdinalIgnoreCase);
}
