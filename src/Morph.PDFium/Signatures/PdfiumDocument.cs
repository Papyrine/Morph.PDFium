namespace Morph.PDFium;

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
