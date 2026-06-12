namespace Morph.PDFium;

/// <summary>
/// A digital signature in the document. Reading the signature exposes its metadata and the
/// raw signed contents; verifying the cryptographic signature is left to the caller (PDFium
/// does not validate signatures).
/// </summary>
/// <param name="Reason">The human-readable reason for signing, when present.</param>
/// <param name="SubFilter">The signature encoding (e.g. "adbe.pkcs7.detached"), when present.</param>
/// <param name="SigningTime">The raw signing time (PDF date string), when present in the dictionary.</param>
/// <param name="Contents">The signature value — a DER-encoded PKCS#1 or PKCS#7 blob.</param>
/// <param name="ByteRange">Pairs of (offset, length) describing the byte ranges the signature covers.</param>
/// <param name="DocMdpPermission">The DocMDP permission level (1-3), or null when not a certifying signature.</param>
public sealed record PdfSignature(
    string? Reason,
    string? SubFilter,
    string? SigningTime,
    byte[] Contents,
    int[] ByteRange,
    uint? DocMdpPermission);

public sealed partial class PdfiumDocument
{
    /// <summary>The number of digital signatures in the document.</summary>
    public int SignatureCount
    {
        get
        {
            var doc = Handle;
            lock (PdfiumNative.Sync)
            {
                return Math.Max(0, PdfiumNative.FPDF_GetSignatureCount(doc));
            }
        }
    }

    /// <summary>The digital signatures in the document, read into managed records.</summary>
    public IReadOnlyList<PdfSignature> GetSignatures()
    {
        var doc = Handle;
        lock (PdfiumNative.Sync)
        {
            var count = PdfiumNative.FPDF_GetSignatureCount(doc);
            if (count <= 0)
            {
                return [];
            }

            var signatures = new List<PdfSignature>(count);
            for (var index = 0; index < count; index++)
            {
                var signature = PdfiumNative.FPDF_GetSignatureObject(doc, index);
                if (signature == IntPtr.Zero)
                {
                    continue;
                }

                var reason = Interop.Utf16ByLength((buffer, length) => PdfiumNative.FPDFSignatureObj_GetReason(signature, buffer, length));
                var subFilter = Interop.Utf8ByLength((buffer, length) => PdfiumNative.FPDFSignatureObj_GetSubFilter(signature, buffer, length));
                var time = Interop.Utf8ByLength((buffer, length) => PdfiumNative.FPDFSignatureObj_GetTime(signature, buffer, length));
                var contents = ReadBytes((buffer, length) => PdfiumNative.FPDFSignatureObj_GetContents(signature, buffer, length));
                var byteRange = ReadByteRange(signature);
                var permission = PdfiumNative.FPDFSignatureObj_GetDocMDPPermission(signature);

                signatures.Add(new(reason, subFilter, time, contents, byteRange, permission == 0 ? null : permission));
            }

            return signatures;
        }
    }

    static byte[] ReadBytes(Interop.LengthDelegate call)
    {
        var length = call([], 0);
        if (length == 0)
        {
            return [];
        }

        var buffer = new byte[length];
        call(buffer, length);
        return buffer;
    }

    static int[] ReadByteRange(IntPtr signature)
    {
        var length = PdfiumNative.FPDFSignatureObj_GetByteRange(signature, [], 0);
        if (length == 0)
        {
            return [];
        }

        var buffer = new int[length];
        PdfiumNative.FPDFSignatureObj_GetByteRange(signature, buffer, length);
        return buffer;
    }
}
