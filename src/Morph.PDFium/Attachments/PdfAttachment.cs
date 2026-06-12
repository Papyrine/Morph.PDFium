namespace Morph.PDFium;

/// <summary>
/// An embedded file attachment. The handle stays valid for the lifetime of the owning
/// document, so instances need not be disposed.
/// </summary>
public sealed class PdfAttachment
{
    readonly IntPtr document;
    readonly IntPtr handle;

    internal PdfAttachment(IntPtr document, IntPtr handle)
    {
        this.document = document;
        this.handle = handle;
    }

    /// <summary>The attachment's file name.</summary>
    public string Name
    {
        get
        {
            lock (PdfiumNative.Sync)
            {
                return Interop.Utf16ByLength((buffer, length) => PdfiumNative.FPDFAttachment_GetName(handle, buffer, length)) ?? string.Empty;
            }
        }
    }

    /// <summary>The attachment's MIME type (the embedded file's /Subtype), or null when unspecified.</summary>
    public string? Subtype
    {
        get
        {
            lock (PdfiumNative.Sync)
            {
                return Interop.Utf16ByLength((buffer, length) => PdfiumNative.FPDFAttachment_GetSubtype(handle, buffer, length));
            }
        }
    }

    /// <summary>The raw bytes of the embedded file, or an empty array when it has no data.</summary>
    public byte[] GetData()
    {
        lock (PdfiumNative.Sync)
        {
            if (!PdfiumNative.FPDFAttachment_GetFile(handle, [], 0, out var length) || length == 0)
            {
                return [];
            }

            var buffer = new byte[length];
            if (!PdfiumNative.FPDFAttachment_GetFile(handle, buffer, length, out _))
            {
                return [];
            }

            return buffer;
        }
    }

    /// <summary>Replaces the embedded file's data.</summary>
    public void SetData(ReadOnlySpan<byte> contents)
    {
        lock (PdfiumNative.Sync)
        {
            if (!PdfiumNative.FPDFAttachment_SetFile(handle, document, contents, (uint) contents.Length))
            {
                throw new PdfiumException($"Failed to set data for attachment '{Name}'");
            }
        }
    }
}