/// <summary>
/// Marshalling helpers shared across the PDFium bindings. PDFium hands strings back
/// through a "call twice" protocol: the first call (with a null/empty buffer) returns
/// the required size, the second call fills a caller-allocated buffer. These helpers
/// centralise that dance for the two encodings PDFium uses (UTF-16LE and UTF-8).
///
/// Every helper here assumes the caller already holds <see cref="PdfiumNative.Sync"/>,
/// since the underlying native calls are not thread safe.
/// </summary>
static class Interop
{
    /// <summary>
    /// Reads a UTF-16LE string via the call-twice protocol where the length is
    /// returned in <em>bytes</em> including the two byte terminator (the convention
    /// used by FPDF_GetMetaText, FPDFBookmark_GetTitle, FPDFAction_GetURIPath, ...).
    /// Returns null when the value is absent or empty.
    /// </summary>
    public static string? Utf16ByLength(LengthDelegate call)
    {
        var length = call([], 0);
        if (length <= 2)
        {
            return null;
        }

        var buffer = new byte[length];
        call(buffer, length);
        var value = Encoding.Unicode.GetString(buffer, 0, (int) length - 2);
        return value.Length == 0 ? null : value;
    }

    /// <summary>
    /// Reads a UTF-8 string via the call-twice protocol where the length is returned
    /// in bytes including the trailing NUL (FPDFAction_GetFilePath, FPDFText_GetFontInfo,
    /// FPDFAttachment_GetName encodes UTF-16 instead — see <see cref="Utf16ByLength"/>).
    /// Returns null when the value is absent or empty.
    /// </summary>
    public static string? Utf8ByLength(LengthDelegate call)
    {
        var length = call([], 0);
        if (length <= 1)
        {
            return null;
        }

        var buffer = new byte[length];
        call(buffer, length);
        var value = Encoding.UTF8.GetString(buffer, 0, (int) length - 1);
        return value.Length == 0 ? null : value;
    }

    /// <summary>
    /// Reads a UTF-16LE string from an API that counts <em>UTF-16 code units</em>
    /// (not bytes) and whose count excludes the terminator (FPDFText_GetText,
    /// FPDFText_GetBoundedText, FPDFLink_GetURL). <paramref name="units"/> is the
    /// code-unit count reported by the API; the buffer it fills holds that many units
    /// plus a terminator.
    /// </summary>
    public static string? Utf16ByUnits(int units, GetUnitsDelegate call)
    {
        if (units <= 0)
        {
            return null;
        }

        // +1 for the terminator the API writes when room is available.
        var buffer = new ushort[units + 1];
        var written = call(buffer, units + 1);
        if (written <= 0)
        {
            return null;
        }

        // written includes the terminator; trim it.
        var chars = MemoryMarshal.Cast<ushort, char>(buffer.AsSpan(0, written - 1));
        var value = new string(chars);
        return value.Length == 0 ? null : value;
    }

    /// <summary>Converts a managed string to a NUL terminated UTF-16LE byte block (FPDF_WIDESTRING).</summary>
    public static byte[] ToWideString(string value)
    {
        var bytes = new byte[(value.Length + 1) * 2];
        Encoding.Unicode.GetBytes(value, bytes);
        return bytes;
    }

    /// <summary>Fills a buffer of <paramref name="length"/> bytes; returns the required byte count.</summary>
    public delegate uint LengthDelegate(Span<byte> buffer, uint length);

    /// <summary>Fills a UTF-16 code-unit buffer; returns the number of code units written.</summary>
    public delegate int GetUnitsDelegate(Span<ushort> buffer, int length);
}

/// <summary>Mirrors the native FS_RECTF struct (a float rectangle in page or device space).</summary>
[StructLayout(LayoutKind.Sequential)]
struct FsRectF
{
    public float Left;
    public float Top;
    public float Right;
    public float Bottom;
}

/// <summary>Mirrors the native FS_MATRIX struct ([a b c d e f] transform).</summary>
[StructLayout(LayoutKind.Sequential)]
struct FsMatrix
{
    public float A;
    public float B;
    public float C;
    public float D;
    public float E;
    public float F;
}

/// <summary>Mirrors the native FS_QUADPOINTSF struct (four corner points).</summary>
[StructLayout(LayoutKind.Sequential)]
struct FsQuadPoints
{
    public float X1;
    public float Y1;
    public float X2;
    public float Y2;
    public float X3;
    public float Y3;
    public float X4;
    public float Y4;
}
