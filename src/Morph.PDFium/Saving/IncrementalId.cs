using System.Text.RegularExpressions;
using Morph.PDFium;

/// <summary>
/// Overrides a PDF's trailer <c>/ID</c> by appending an incremental-update section, rather than
/// rewriting bytes in place. PDFium regenerates <c>/ID</c> on every save (preserving the source's
/// permanent id, randomising the changing id) and exposes no setter, so this is the only way to
/// pin a specific identifier. The appended section adds no objects: a minimal cross-reference
/// subsection plus a new trailer that carries <c>/Root</c>, <c>/Size</c> and <c>/Info</c> forward,
/// points <c>/Prev</c> at the previous cross-reference, and supplies the new <c>/ID</c>. Because it
/// only concatenates at the end, existing byte offsets stay valid.
/// </summary>
static class IncrementalId
{
    static readonly Regex rootPattern = new(@"/Root\s+(\d+\s+\d+\s+R)", RegexOptions.CultureInvariant);
    static readonly Regex sizePattern = new(@"/Size\s+(\d+)", RegexOptions.CultureInvariant);
    static readonly Regex infoPattern = new(@"/Info\s+(\d+\s+\d+\s+R)", RegexOptions.CultureInvariant);

    /// <summary>
    /// Writes <paramref name="original"/> to <paramref name="destination"/> followed by an
    /// incremental-update section that replaces the trailer <c>/ID</c> with
    /// <paramref name="permanent"/> and <paramref name="changing"/>.
    /// </summary>
    public static void Append(ReadOnlySpan<byte> original, Stream destination, byte[] permanent, byte[] changing)
    {
        var startxref = original.LastIndexOf("startxref"u8);
        var trailer = original.LastIndexOf("trailer"u8);
        if (startxref < 0 || trailer < 0 || trailer >= startxref)
        {
            throw new NotSupportedException("Cannot override /ID: the document has no classic trailer (cross-reference streams are not supported).");
        }

        var previousXref = ParseOffsetAfter(original, startxref + "startxref".Length);

        // The trailer dictionary sits between the `trailer` keyword and the following `startxref`.
        var dictionary = Encoding.Latin1.GetString(original[trailer..startxref]);
        if (dictionary.Contains("/Encrypt", StringComparison.Ordinal))
        {
            throw new NotSupportedException("Cannot override /ID on an encrypted document; /ID participates in the encryption key. Save with SaveFlags.RemoveSecurity first.");
        }

        var root = rootPattern.Match(dictionary);
        var size = sizePattern.Match(dictionary);
        if (!root.Success || !size.Success)
        {
            throw new PdfiumException("Could not locate /Root and /Size in the document trailer");
        }

        var info = infoPattern.Match(dictionary);

        destination.Write(original);

        // The `xref` keyword starts one byte (the separating newline) past the original content.
        var newXref = (long) original.Length + 1;

        var builder = new StringBuilder();
        builder.Append('\n');
        // A section that changes nothing but the trailer: object 0 free-list head only.
        builder.Append("xref\n0 1\n0000000000 65535 f\r\n");
        builder.Append("trailer\n<< /Size ").Append(size.Groups[1].Value);
        builder.Append(" /Root ").Append(root.Groups[1].Value);
        if (info.Success)
        {
            builder.Append(" /Info ").Append(info.Groups[1].Value);
        }

        builder.Append(" /Prev ").Append(previousXref);
        builder.Append(" /ID[<").Append(Convert.ToHexString(permanent)).Append("><").Append(Convert.ToHexString(changing)).Append(">] >>\n");
        builder.Append("startxref\n").Append(newXref).Append("\n%%EOF\n");

        destination.Write(Encoding.Latin1.GetBytes(builder.ToString()));
    }

    static long ParseOffsetAfter(ReadOnlySpan<byte> data, int start)
    {
        var index = start;
        while (index < data.Length && data[index] is (byte) ' ' or (byte) '\r' or (byte) '\n' or (byte) '\t')
        {
            index++;
        }

        long value = 0;
        var any = false;
        while (index < data.Length && data[index] is >= (byte) '0' and <= (byte) '9')
        {
            value = (value * 10) + (data[index] - (byte) '0');
            index++;
            any = true;
        }

        if (!any)
        {
            throw new PdfiumException("Could not parse the startxref offset from the document trailer");
        }

        return value;
    }
}
