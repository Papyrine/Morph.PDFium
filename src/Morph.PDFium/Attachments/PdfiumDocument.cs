namespace Morph.PDFium;

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
