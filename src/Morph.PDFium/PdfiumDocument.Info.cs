namespace Morph.PDFium;

public sealed partial class PdfiumDocument
{
    /// <summary>
    /// The PDF specification version the document declares, e.g. "1.7" or "2.0".
    /// Returns null when the version cannot be determined (PDFium reports none for
    /// documents created in memory rather than parsed from a file).
    /// </summary>
    public string? GetPdfVersion()
    {
        var doc = Handle;
        lock (PdfiumNative.Sync)
        {
            // PDFium encodes the version as an integer: 14 => "1.4", 17 => "1.7", 20 => "2.0".
            if (!PdfiumNative.FPDF_GetFileVersion(doc, out var version) || version <= 0)
            {
                return null;
            }

            return $"{version / 10}.{version % 10}";
        }
    }

    /// <summary>
    /// The access permissions granted by the document. For an unprotected document, or
    /// one opened with the owner password, this is <see cref="DocumentPermissions.All"/>.
    /// Use <see cref="GetUserPermissions"/> to always see the user-level restrictions even
    /// when the owner password was supplied.
    /// </summary>
    public DocumentPermissions GetPermissions()
    {
        var doc = Handle;
        lock (PdfiumNative.Sync)
        {
            return (DocumentPermissions) PdfiumNative.FPDF_GetDocPermissions(doc);
        }
    }

    /// <summary>
    /// The user-level access permissions, reported even when the document was unlocked with
    /// the owner password. Returns <see cref="DocumentPermissions.All"/> for an unprotected
    /// document.
    /// </summary>
    public DocumentPermissions GetUserPermissions()
    {
        var doc = Handle;
        lock (PdfiumNative.Sync)
        {
            return (DocumentPermissions) PdfiumNative.FPDF_GetDocUserPermissions(doc);
        }
    }

    /// <summary>
    /// The revision of the document's standard security handler, or null when the document
    /// is not encrypted. Higher revisions correspond to stronger encryption (revision 2/3 RC4,
    /// 5/6 AES).
    /// </summary>
    public int? GetSecurityHandlerRevision()
    {
        var doc = Handle;
        lock (PdfiumNative.Sync)
        {
            var revision = PdfiumNative.FPDF_GetSecurityHandlerRevision(doc);
            return revision < 0 ? null : revision;
        }
    }

    /// <summary>The page mode declared in the document catalog, controlling the initial view layout.</summary>
    public PageDisplayMode GetPageDisplayMode()
    {
        var doc = Handle;
        lock (PdfiumNative.Sync)
        {
            return (PageDisplayMode) PdfiumNative.FPDFDoc_GetPageMode(doc);
        }
    }

    /// <summary>
    /// The page label for the page at <paramref name="index"/> — the human-facing page
    /// "number" (which may be roman numerals, prefixed, etc.), or null when the document
    /// defines no label for the page.
    /// </summary>
    public string? GetPageLabel(int index)
    {
        var doc = Handle;
        ValidateIndex(index);
        lock (PdfiumNative.Sync)
        {
            return Interop.Utf16ByLength((buffer, length) => PdfiumNative.FPDF_GetPageLabel(doc, index, buffer, length));
        }
    }

    /// <summary>
    /// The trailer file identifier of the requested <paramref name="type"/> as a byte string
    /// rendered in lowercase hexadecimal, or null when the document has no such identifier.
    /// </summary>
    public string? GetFileIdentifier(FileIdentifierType type = FileIdentifierType.Permanent)
    {
        var doc = Handle;
        lock (PdfiumNative.Sync)
        {
            var length = PdfiumNative.FPDF_GetFileIdentifier(doc, (int) type, [], 0);
            if (length <= 1)
            {
                return null;
            }

            var buffer = new byte[length];
            PdfiumNative.FPDF_GetFileIdentifier(doc, (int) type, buffer, length);
            // The identifier is a raw byte string (not text); expose it as hex. Drop the NUL.
            return Convert.ToHexStringLower(buffer, 0, (int) length - 1);
        }
    }
}
