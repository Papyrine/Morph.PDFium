using System.Globalization;
using Morph.PDFium;

/// <summary>
/// Writes pages and documents out as HTML. Pure managed work over what was read from PDFium,
/// so it runs outside the PDFium lock. Lines end in "\n" on every platform, so the output does
/// not depend on where it was made.
/// </summary>
static class HtmlWriter
{
    const string styles =
        """
        body{margin:0;background:#525659;font-family:system-ui,sans-serif}
        .pdf-outline{box-sizing:border-box;max-width:900px;margin:16px auto 0;padding:8px 24px;background:#fff;border-radius:4px}
        .pdf-outline ul{margin:0;padding-left:20px}
        .pdf-outline>ul{padding-left:0;list-style:none}
        .pdf-outline a{color:#1a5fb4;text-decoration:none}
        .pdf-outline a:hover{text-decoration:underline}
        .pdf-document{display:flex;flex-direction:column;align-items:center;gap:16px;padding:16px}
        .pdf-page{box-shadow:0 1px 4px rgba(0,0,0,.5)}
        .pdf-page ::selection{background:rgba(0,100,255,.3)}
        .pdf-overlay ::selection{fill:transparent}
        @page{margin:0}
        @media print{body{background:none}.pdf-outline{display:none}.pdf-document{display:block;padding:0}.pdf-page{box-shadow:none;break-after:page}}
        """;

    // The name of the printed page a page of this size goes on: its style asks for it with
    // the page property, and the document declares one @page rule of that size per name. In a
    // fragment placed on its own, the name matches no rule, and the page prints on whatever
    // sheet the browser chooses.
    static string PageName(CssNumber width, CssNumber height) =>
        $"pdf-{width}x{height}".Replace('.', '_');

    const string dataUriPrefix = "data:image/png;base64,";

    // The page's image is either imageUrl, the caller's URL for it, or png, embedded as a data
    // URI; or neither, for a page without its image.
    public static string Page(HtmlPageLayout layout, string? imageUrl, byte[]? png, HtmlOptions options)
    {
        var base64Length = png is null ? 0 : (png.Length + 2) / 3 * 4;
        var builder = new StringBuilder(512 + layout.Runs.Count * 160 + (imageUrl?.Length ?? 0) + base64Length);
        var width = Number(layout.Width);
        var height = Number(layout.Height);
        builder.Append("<div class=\"pdf-page\" id=\"");
        AppendEscaped(builder, PageId(options.PageIdPrefix, layout.Index));
        builder.Append($"\" style=\"position:relative;overflow:hidden;width:{width}pt;max-width:100%;aspect-ratio:{width}/{height};background:{Color(options.Render.Background)};page:{PageName(width, height)}\">\n");

        if (imageUrl is not null || png is not null)
        {
            builder.Append("<img src=\"");
            if (png is null)
            {
                AppendEscaped(builder, imageUrl!);
            }
            else
            {
                // Base64 has nothing to escape, so it is encoded straight into the builder,
                // rather than through a string the size of the image and then escaped
                // character by character.
                builder.Append(dataUriPrefix);
                var chars = ArrayPool<char>.Shared.Rent(base64Length);
                Convert.TryToBase64Chars(png, chars, out var written);
                builder.Append(chars, 0, written);
                ArrayPool<char>.Shared.Return(chars);
            }

            // With text the image is decoration over which the text is read; without it the
            // image is all there is, so it says which page it is.
            var alt = options.Text == HtmlTextMode.None ? $"Page {layout.Index + 1}" : "";
            builder.Append($"\" alt=\"{alt}\"");
            // The first page is on screen as soon as the HTML opens, so only later pages wait
            // until they are scrolled near.
            if (options.LazyLoadImages && layout.Index > 0)
            {
                builder.Append(" loading=\"lazy\" decoding=\"async\"");
            }

            builder.Append(" draggable=\"false\" style=\"position:absolute;left:0;top:0;width:100%;height:100%;user-select:none\">\n");
        }

        if (layout.Runs.Count > 0 || layout.Links.Count > 0)
        {
            builder.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {width} {height}\" preserveAspectRatio=\"none\" xml:space=\"preserve\" style=\"position:absolute;left:0;top:0;width:100%;height:100%;white-space:pre\"");
            if (options.Text == HtmlTextMode.Visible)
            {
                builder.Append(" fill=\"#000\">\n");
            }
            else
            {
                builder.Append(" class=\"pdf-overlay\" fill=\"transparent\" font-family=\"sans-serif\">\n");
            }

            foreach (var run in layout.Runs)
            {
                AppendRun(builder, run);
            }

            foreach (var link in layout.Links)
            {
                builder.Append("<a href=\"");
                AppendEscaped(builder, link.Href);
                builder.Append("\">");
                foreach (var rectangle in link.Rectangles)
                {
                    builder.Append($"<rect x=\"{Number(rectangle.X)}\" y=\"{Number(rectangle.Y)}\" width=\"{Number(rectangle.Width)}\" height=\"{Number(rectangle.Height)}\" fill=\"transparent\"/>");
                }

                builder.Append("</a>\n");
            }

            builder.Append("</svg>\n");
        }

        builder.Append("</div>\n");
        return builder.ToString();
    }

    static void AppendRun(StringBuilder builder, HtmlTextRun run)
    {
        var x = Number(run.X);
        var y = Number(run.Y);
        builder.Append($"<text x=\"{x}\" y=\"{y}\" font-size=\"{Number(run.FontSize)}\"");
        if (run.Length > 0)
        {
            builder.Append($" textLength=\"{Number(run.Length)}\" lengthAdjust=\"spacingAndGlyphs\"");
        }

        if (run.RightToLeft)
        {
            // Started at its right-hand end: so laid out, the browser also places neutral
            // characters, such as a full stop, where right-to-left text has them.
            builder.Append(" direction=\"rtl\"");
        }

        var angle = Number(run.Angle);
        if (!angle.IsZero || run.Mirrored)
        {
            builder.Append(" transform=\"");
            if (!angle.IsZero)
            {
                builder.Append($"rotate({angle} {x} {y})");
            }

            if (run.Mirrored)
            {
                if (!angle.IsZero)
                {
                    builder.Append(' ');
                }

                // Flipped across the baseline about the run's start, before it is turned.
                builder.Append($"translate({x} {y}) scale(1 -1) translate({Number(-run.X)} {Number(-run.Y)})");
            }

            builder.Append('"');
        }

        var style = run.Style;
        if (style.Generic is not null)
        {
            if (style.Family is null)
            {
                builder.Append($" font-family=\"{style.Generic}\"");
            }
            else
            {
                builder.Append($" font-family=\"'{style.Family}', {style.Generic}\"");
            }

            if (style.Weight != 400)
            {
                builder.Append($" font-weight=\"{style.Weight.ToString(CultureInfo.InvariantCulture)}\"");
            }

            if (style.Italic)
            {
                builder.Append(" font-style=\"italic\"");
            }

            if (style.Fill is { } fill)
            {
                if (fill.R != 0 || fill.G != 0 || fill.B != 0)
                {
                    builder.Append($" fill=\"#{fill.R:x2}{fill.G:x2}{fill.B:x2}\"");
                }

                if (fill.A != 255)
                {
                    builder.Append($" fill-opacity=\"{Number(fill.A / 255d)}\"");
                }
            }
            else
            {
                builder.Append(" fill=\"none\"");
            }

            if (style.Stroke is { } stroke)
            {
                // A width of 0 is the thinnest line the device can draw: about a pixel.
                var width = style.StrokeWidth > 0 ? style.StrokeWidth : 0.75;
                builder.Append($" stroke=\"#{stroke.R:x2}{stroke.G:x2}{stroke.B:x2}\" stroke-width=\"{Number(width)}\"");
                if (stroke.A != 255)
                {
                    builder.Append($" stroke-opacity=\"{Number(stroke.A / 255d)}\"");
                }
            }
        }

        builder.Append('>');
        AppendEscaped(builder, run.Text);
        builder.Append("</text>\n");
    }

    public const string DocumentEnd = "</main>\n</body>\n</html>\n";

    // Everything before the pages: the head, the outline and the opening of the main element.
    // DocumentEnd follows the pages.
    public static void DocumentStart(TextWriter writer, string title, string? language, Dictionary<string, string>? properties, IReadOnlyList<PdfBookmark> outline, IReadOnlyList<PageSize> sizes, HtmlOptions options)
    {
        var builder = new StringBuilder("<!DOCTYPE html>\n<html");
        if (language is not null && IsLanguageTag(language))
        {
            builder.Append($" lang=\"{language}\"");
        }

        builder.Append(">\n<head>\n<meta charset=\"utf-8\">\n<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n<meta name=\"generator\" content=\"Morph.PDFium\">\n");
        AppendMeta(builder, "author", properties, "Author");
        AppendMeta(builder, "description", properties, "Subject");
        AppendMeta(builder, "keywords", properties, "Keywords");
        builder.Append("<title>");
        AppendEscaped(builder, title);
        builder.Append($"</title>\n<style>\n{styles}\n");
        // One printed page size per page size in the document, so each page prints on a sheet
        // its own size rather than the browser's default paper, which would shrink it or run
        // it onto a second sheet.
        var names = new HashSet<string>();
        foreach (var size in sizes)
        {
            var width = Number(size.Width);
            var height = Number(size.Height);
            var name = PageName(width, height);
            if (names.Add(name))
            {
                builder.Append($"@page {name}{{size:{width}pt {height}pt}}\n");
            }
        }

        builder.Append("</style>\n</head>\n<body>\n");

        if (outline.Count > 0)
        {
            builder.Append("<nav class=\"pdf-outline\" aria-label=\"Outline\">\n");
            AppendOutline(builder, outline, options);
            builder.Append("</nav>\n");
        }

        builder.Append("<main class=\"pdf-document\">\n");
        writer.Write(builder);
    }

    static void AppendMeta(StringBuilder builder, string name, Dictionary<string, string>? properties, string property)
    {
        if (properties is null ||
            !properties.TryGetValue(property, out var value))
        {
            return;
        }

        builder.Append($"<meta name=\"{name}\" content=\"");
        AppendEscaped(builder, value);
        builder.Append("\">\n");
    }

    static void AppendOutline(StringBuilder builder, IReadOnlyList<PdfBookmark> bookmarks, HtmlOptions options)
    {
        builder.Append("<ul>\n");
        foreach (var bookmark in bookmarks)
        {
            builder.Append("<li>");
            var href = Href(bookmark.Destination, bookmark.Action, options);
            if (href is null)
            {
                builder.Append("<span>");
                AppendEscaped(builder, bookmark.Title);
                builder.Append("</span>");
            }
            else
            {
                builder.Append("<a href=\"");
                AppendEscaped(builder, href);
                builder.Append("\">");
                AppendEscaped(builder, bookmark.Title);
                builder.Append("</a>");
            }

            if (bookmark.Children.Count > 0)
            {
                builder.Append('\n');
                AppendOutline(builder, bookmark.Children, options);
            }

            builder.Append("</li>\n");
        }

        builder.Append("</ul>\n");
    }

    static string PageId(string prefix, int index) =>
        prefix + (index + 1).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Where a link or bookmark should point: a page of this document by its id, a URL, or a
    /// file beside this one. Null for a target HTML cannot reach, or should not: a launch, a
    /// file embedded in the PDF, a URL that <see cref="SafeUrl"/> rejects, or a page past
    /// <see cref="HtmlOptions.MaxPages"/>.
    /// </summary>
    public static string? Href(PdfDestination? destination, PdfAction? action, HtmlOptions options)
    {
        if (destination is { PageIndex: >= 0 } local)
        {
            return PageHref(local.PageIndex, options);
        }

        if (action is not { } target)
        {
            return null;
        }

        return target.Type switch
        {
            PdfActionType.Uri => SafeUrl(target.Uri, options),
            PdfActionType.GoTo when target.Destination is { PageIndex: >= 0 } goTo => PageHref(goTo.PageIndex, options),
            PdfActionType.RemoteGoTo => RemoteFileUrl(target.FilePath, options),
            _ => null
        };
    }

    static string? PageHref(int index, HtmlOptions options)
    {
        if (index >= options.MaxPages)
        {
            return null;
        }

        return "#" + PageId(options.PageIdPrefix, index);
    }

    /// <summary>
    /// The file a remote go-to opens, as a link relative to the HTML, or null. PDFium decodes the
    /// PDF's file specification into a path for the OS it runs on: on Windows "/" becomes "\",
    /// and an absolute "/volume/path" becomes "\\volume\path" or "c:\path"; on macOS "/" becomes
    /// ":"; elsewhere it is left as the PDF wrote it. So the separators are turned back into "/",
    /// making the HTML the same whichever OS wrote it, and an absolute path, which names a
    /// volume, a drive or a host rather than a file beside the PDF, is never a link. A URL, which
    /// a /FS /URL specification gives as written, is told by its "://" and left to
    /// <see cref="SafeUrl"/>.
    /// </summary>
    static string? RemoteFileUrl(string? path, HtmlOptions options)
    {
        if (path is null)
        {
            return null;
        }

        if (!path.Contains("://"))
        {
            if (OperatingSystem.IsMacOS())
            {
                path = path.Replace(':', '/');
            }

            path = path.Replace('\\', '/');
        }

        var url = SafeUrl(path, options);
        if (url is null || url.StartsWith('/'))
        {
            return null;
        }

        return url;
    }

    /// <summary>
    /// The URL with control characters and surrounding white space removed, or null when it is
    /// empty or has a scheme not in <see cref="HtmlOptions.AllowedLinkSchemes"/>. A URL with no
    /// scheme is a relative reference, kept only with <see cref="HtmlOptions.AllowRelativeLinks"/>.
    /// </summary>
    public static string? SafeUrl(string? url, HtmlOptions options)
    {
        if (url is null)
        {
            return null;
        }

        var builder = new StringBuilder(url.Length);
        foreach (var character in url)
        {
            if (!char.IsControl(character))
            {
                builder.Append(character);
            }
        }

        var cleaned = builder.ToString().Trim();
        if (cleaned.Length == 0)
        {
            return null;
        }

        // A scheme is whatever comes before the first colon, provided that colon comes before
        // any path, query or fragment. Anything that is not a valid scheme name there, such as
        // the drive of "C:\file.pdf", is rejected along with the unsafe schemes.
        var colon = cleaned.IndexOf(':');
        var delimiter = cleaned.IndexOfAny(['/', '?', '#']);
        if (colon < 0 ||
            (delimiter >= 0 &&
             delimiter < colon))
        {
            // Two slashes, or backslashes, which browsers read as slashes, in any mix, start a
            // network-path reference: another host, reached with the page's own scheme, or
            // from a file:// page on Windows an SMB share, which can leak the user's
            // credentials. Not a relative link to anything beside the HTML, so never kept.
            if (cleaned is ['/' or '\\', '/' or '\\', ..])
            {
                return null;
            }

            if (options.AllowRelativeLinks)
            {
                return cleaned;
            }

            return null;
        }

        var scheme = cleaned.AsSpan(0, colon);
        foreach (var allowed in options.AllowedLinkSchemes)
        {
            if (scheme.Equals(allowed, StringComparison.OrdinalIgnoreCase))
            {
                return cleaned;
            }
        }

        return null;
    }

    static bool IsLanguageTag(string value)
    {
        foreach (var character in value)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character != '-')
            {
                return false;
            }
        }

        return value.Length > 0;
    }

    // Escapes text for either element content or a double quoted attribute. Characters HTML
    // does not allow (control characters, and unpaired surrogates, which no encoding can
    // write) are replaced or dropped rather than producing a malformed document.
    static void AppendEscaped(StringBuilder builder, string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            switch (character)
            {
                case '&':
                    builder.Append("&amp;");
                    break;
                case '<':
                    builder.Append("&lt;");
                    break;
                case '>':
                    builder.Append("&gt;");
                    break;
                case '"':
                    builder.Append("&quot;");
                    break;
                case '\t' or '\n':
                    builder.Append(character);
                    break;
                // C0 and C1 controls and DEL, and the two noncharacters a UTF-16 unit can hold.
                case < ' ' or >= '\u007F' and <= '\u009F' or '\uFFFE' or '\uFFFF':
                    break;
                default:
                    if (char.IsHighSurrogate(character) &
                        index + 1 < value.Length &&
                        char.IsLowSurrogate(value[index + 1]))
                    {
                        builder.Append(character).Append(value[index + 1]);
                        index++;
                    }
                    else if (char.IsSurrogate(character))
                    {
                        builder.Append('\uFFFD');
                    }
                    else
                    {
                        builder.Append(character);
                    }

                    break;
            }
        }
    }

    static string Color(PdfColor color)
    {
        if (color.A == 255)
        {
            return $"#{color.R:x2}{color.G:x2}{color.B:x2}";
        }

        return $"rgba({color.R},{color.G},{color.B},{Number(color.A / 255d)})";
    }

    static CssNumber Number(double value) =>
        new(value);
}
