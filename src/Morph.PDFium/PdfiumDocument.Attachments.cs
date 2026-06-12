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

public sealed partial class PdfiumDocument
{
    /// <summary>The number of embedded file attachments in the document.</summary>
    public int AttachmentCount
    {
        get
        {
            var doc = Handle;
            lock (PdfiumNative.Sync)
            {
                return Math.Max(0, PdfiumNative.FPDFDoc_GetAttachmentCount(doc));
            }
        }
    }

    /// <summary>The embedded file attachments in the document.</summary>
    public IReadOnlyList<PdfAttachment> GetAttachments()
    {
        var doc = Handle;
        lock (PdfiumNative.Sync)
        {
            var count = PdfiumNative.FPDFDoc_GetAttachmentCount(doc);
            if (count <= 0)
            {
                return [];
            }

            var attachments = new List<PdfAttachment>(count);
            for (var index = 0; index < count; index++)
            {
                var handle = PdfiumNative.FPDFDoc_GetAttachment(doc, index);
                if (handle != IntPtr.Zero)
                {
                    attachments.Add(new(doc, handle));
                }
            }

            return attachments;
        }
    }

    /// <summary>
    /// Adds a new embedded file with the given <paramref name="name"/> and
    /// <paramref name="contents"/>, returning the created attachment. Persist with
    /// <see cref="Save(SaveFlags,int?)"/>.
    /// </summary>
    public PdfAttachment AddAttachment(string name, ReadOnlySpan<byte> contents)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        var doc = Handle;
        var wideName = Interop.ToWideString(name);
        lock (PdfiumNative.Sync)
        {
            var handle = PdfiumNative.FPDFDoc_AddAttachment(doc, wideName);
            if (handle == IntPtr.Zero)
            {
                throw new PdfiumException($"Failed to add attachment '{name}' (duplicate name?)");
            }

            if (!PdfiumNative.FPDFAttachment_SetFile(handle, doc, contents, (uint) contents.Length))
            {
                throw new PdfiumException($"Failed to write data for attachment '{name}'");
            }

            return new(doc, handle);
        }
    }

    /// <summary>Removes the attachment at <paramref name="index"/> from the embedded files name tree.</summary>
    public void DeleteAttachment(int index)
    {
        var doc = Handle;
        lock (PdfiumNative.Sync)
        {
            if (!PdfiumNative.FPDFDoc_DeleteAttachment(doc, index))
            {
                throw new PdfiumException($"Failed to delete attachment at index {index}");
            }
        }
    }
}
