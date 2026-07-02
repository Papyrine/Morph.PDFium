namespace Morph.PDFium;

public sealed partial class PdfiumDocument
{
    // A trailer /ID override set via SetFileIdentifier, applied on the next Save. Both elements
    // are set together, so a non-null permanent implies a non-null changing.
    byte[]? idPermanent;
    byte[]? idChanging;

    /// <summary>
    /// Pins the trailer file identifier (<c>/ID</c>) used on the next <see cref="Save(SaveFlags,int?)"/>,
    /// setting both the permanent and changing elements to <paramref name="identifier"/>. Useful when you
    /// need a specific or reproducible identifier, since PDFium otherwise randomises the changing element
    /// on every save. Note this pins the effective <c>/ID</c> only; it does not make the whole file
    /// byte-for-byte identical across saves.
    /// </summary>
    public void SetFileIdentifier(byte[] identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        SetFileIdentifier(identifier, identifier);
    }

    /// <summary>
    /// Pins the two trailer file identifier (<c>/ID</c>) elements used on the next
    /// <see cref="Save(SaveFlags,int?)"/>. PDFium exposes no <c>/ID</c> setter and rewrites the array
    /// itself on save, so the value is applied by appending an incremental-update trailer to the saved
    /// bytes. Not supported for encrypted documents (the identifier participates in the encryption key);
    /// save with <see cref="SaveFlags.RemoveSecurity"/> first if needed.
    /// </summary>
    public void SetFileIdentifier(byte[] permanent, byte[] changing)
    {
        ArgumentNullException.ThrowIfNull(permanent);
        ArgumentNullException.ThrowIfNull(changing);
        if (permanent.Length == 0 || changing.Length == 0)
        {
            throw new ArgumentException("File identifier elements must be non-empty");
        }

        ThrowIfDisposed();
        idPermanent = (byte[]) permanent.Clone();
        idChanging = (byte[]) changing.Clone();
    }

    /// <summary>Serialises the (possibly modified) document to a new byte array.</summary>
    public byte[] Save(SaveFlags flags = SaveFlags.None, int? pdfVersion = null)
    {
        using var stream = new MemoryStream();
        Save(stream, flags, pdfVersion);
        return stream.ToArray();
    }

    /// <summary>
    /// Serialises the (possibly modified) document to <paramref name="stream"/>. Pass
    /// <paramref name="pdfVersion"/> as e.g. 17 for "1.4"/"1.7" to pin the output version.
    /// </summary>
    public void Save(Stream stream, SaveFlags flags = SaveFlags.None, int? pdfVersion = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (idPermanent is null)
        {
            WritePdfium(stream, flags, pdfVersion);
            return;
        }

        // The /ID override appends an incremental trailer, which needs the full content length and
        // the existing trailer, so PDFium's output is buffered first and then rewritten to `stream`.
        using var buffer = new MemoryStream();
        WritePdfium(buffer, flags, pdfVersion);
        IncrementalId.Append(buffer.GetBuffer().AsSpan(0, (int) buffer.Length), stream, idPermanent, idChanging!);
    }

    // Writes PDFium's serialised output straight to `target` via the FPDF_FILEWRITE callback.
    unsafe void WritePdfium(Stream target, SaveFlags flags, int? pdfVersion)
    {
        var doc = Handle;
        var context = GCHandle.Alloc(target);
        try
        {
            var writer = new PdfiumNative.FileWrite
            {
                Version = 1,
                WriteBlock = (IntPtr) (delegate* unmanaged[Cdecl]<PdfiumNative.FileWrite*, void*, uint, int>) &WriteBlock,
                Context = GCHandle.ToIntPtr(context)
            };

            lock (PdfiumNative.Sync)
            {
                var ok = pdfVersion is { } version
                    ? PdfiumNative.FPDF_SaveWithVersion(doc, in writer, (uint) flags, version)
                    : PdfiumNative.FPDF_SaveAsCopy(doc, in writer, (uint) flags);
                if (!ok)
                {
                    throw new PdfiumException("Failed to save document");
                }
            }
        }
        finally
        {
            context.Free();
        }
    }

    // Invoked by PDFium for each block of output. `self` points at the FileWrite struct we
    // passed in; its Context slot carries the GCHandle to the destination stream.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    static unsafe int WriteBlock(PdfiumNative.FileWrite* self, void* data, uint size)
    {
        if (GCHandle.FromIntPtr(self->Context).Target is not Stream stream)
        {
            return 0;
        }

        try
        {
            stream.Write(new(data, (int) size));
            return 1;
        }
        catch
        {
            // Surfacing managed exceptions across the native boundary is undefined; signal
            // failure to PDFium instead and let FPDF_SaveAsCopy report it as a failed save.
            return 0;
        }
    }
}
