namespace Morph.PDFium;

public sealed partial class PdfiumDocument
{
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
    public unsafe void Save(Stream stream, SaveFlags flags = SaveFlags.None, int? pdfVersion = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var doc = Handle;
        var state = new SaveState(stream);
        var context = GCHandle.Alloc(state);
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
                // PDFium can report success even though a write was refused, so a captured
                // stream exception decides the outcome ahead of the native result.
                state.Error?.Throw();
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

    // The destination stream plus the exception, if any, that it threw part way through a save.
    sealed class SaveState(Stream stream)
    {
        public Stream Stream { get; } = stream;
        public ExceptionDispatchInfo? Error { get; set; }
    }

    // Invoked by PDFium for each block of output. `self` points at the FileWrite struct we
    // passed in; its Context slot carries the GCHandle to the save state.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    static unsafe int WriteBlock(PdfiumNative.FileWrite* self, void* data, uint size)
    {
        if (GCHandle.FromIntPtr(self->Context).Target is not SaveState state)
        {
            return 0;
        }

        try
        {
            state.Stream.Write(new(data, (int) size));
            return 1;
        }
        catch (Exception exception)
        {
            // Surfacing managed exceptions across the native boundary is undefined; signal
            // failure to PDFium instead and let Save rethrow once the native call returns.
            state.Error ??= ExceptionDispatchInfo.Capture(exception);
            return 0;
        }
    }
}
