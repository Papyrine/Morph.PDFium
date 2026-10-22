namespace Morph.PDFium;

/// <summary>
/// An embedded file attachment. The handle stays valid for the lifetime of the owning
/// document, so instances need not be disposed; using one after its document has been
/// disposed throws <see cref="ObjectDisposedException"/>, and using one after it has been
/// removed with <see cref="PdfiumDocument.DeleteAttachment"/> throws
/// <see cref="InvalidOperationException"/>.
/// </summary>
public sealed class PdfAttachment
{
    readonly PdfiumDocument document;
    readonly IntPtr handle;
    readonly AttachmentToken token;

    internal PdfAttachment(PdfiumDocument document, IntPtr handle, AttachmentToken token)
    {
        this.document = document;
        this.handle = handle;
        this.token = token;
    }

    // Caller must hold PdfiumNative.Sync.
    void ThrowIfUnusable()
    {
        // Touch the document so a disposed document surfaces as ObjectDisposedException.
        _ = document.Handle;
        if (token.Deleted)
        {
            throw new InvalidOperationException("The attachment has been deleted from its document.");
        }
    }

    /// <summary>The attachment's file name.</summary>
    public string Name
    {
        get
        {
            lock (PdfiumNative.Sync)
            {
                ThrowIfUnusable();
                return Interop.Utf16ByLength(handle, static (state, buffer, length) => PdfiumNative.FPDFAttachment_GetName(state, buffer, length)) ?? string.Empty;
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
                ThrowIfUnusable();
                return Interop.Utf16ByLength(handle, static (state, buffer, length) => PdfiumNative.FPDFAttachment_GetSubtype(state, buffer, length));
            }
        }
    }

    /// <summary>The raw bytes of the embedded file, or an empty array when it has no data.</summary>
    public byte[] GetData()
    {
        lock (PdfiumNative.Sync)
        {
            ThrowIfUnusable();
            if (!PdfiumNative.FPDFAttachment_GetFile(handle, [], 0, out var required) || required.Value == 0)
            {
                return [];
            }

            // Decoded a second time: PDFium copies the file only into a buffer of exactly its
            // length, so a larger one, which could have saved the first decode, stays empty.
            var length = checked((uint) required.Value);
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
            var doc = document.Handle;
            ThrowIfUnusable();
            if (!PdfiumNative.FPDFAttachment_SetFile(handle, doc, contents, (uint) contents.Length))
            {
                throw new PdfiumException($"Failed to set data for attachment '{Name}'");
            }
        }
    }
}