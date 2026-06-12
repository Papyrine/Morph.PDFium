using System.Runtime.CompilerServices;

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
        var context = GCHandle.Alloc(stream);
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
            stream.Write(new ReadOnlySpan<byte>(data, (int) size));
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
