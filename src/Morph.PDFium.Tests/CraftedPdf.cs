/// <summary>
/// Builds small PDFs by hand for features the sample files do not contain (rotated pages,
/// offset page boxes, link actions, annotations with appearances). Each test supplies the
/// objects it needs; this writes them out with a valid cross reference table.
/// </summary>
static class CraftedPdf
{
    /// <summary>A Helvetica font dictionary, for use as a page's /F1 resource.</summary>
    public const string Helvetica = "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>";

    /// <summary>A stream object body with its /Length filled in.</summary>
    public static string Stream(string content, string dictionary = "") =>
        $"<< {dictionary} /Length {content.Length} >>\nstream\n{content}\nendstream";

    /// <summary>
    /// Serialises the numbered objects. Object 1 must be the document catalog.
    /// </summary>
    public static byte[] Build(SortedDictionary<int, string> objects)
    {
        var builder = new StringBuilder("%PDF-1.7\n");
        var offsets = new Dictionary<int, int>();
        foreach (var (id, body) in objects)
        {
            offsets[id] = builder.Length;
            builder.Append($"{id} 0 obj\n{body}\nendobj\n");
        }

        var xref = builder.Length;
        var size = objects.Keys.Max() + 1;
        builder.Append($"xref\n0 {size}\n0000000000 65535 f \n");
        for (var id = 1; id < size; id++)
        {
            if (offsets.TryGetValue(id, out var offset))
            {
                builder.Append($"{offset:0000000000} 00000 n \n");
            }
            else
            {
                builder.Append("0000000000 65535 f \n");
            }
        }

        builder.Append($"trailer\n<< /Size {size} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.Latin1.GetBytes(builder.ToString());
    }
}
