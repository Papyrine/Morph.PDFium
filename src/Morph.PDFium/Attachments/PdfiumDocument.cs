namespace Morph.PDFium;

public sealed partial class PdfiumDocument
{
    // Handles removed by DeleteAttachment. PDFium keeps the underlying object readable after
    // it leaves the name tree, so a PdfAttachment for it would otherwise go on returning the
    // deleted file's name and data as though it were still attached.
    readonly HashSet<IntPtr> deletedAttachments = [];

    // Caller must hold PdfiumNative.Sync.
    internal bool IsAttachmentDeleted(IntPtr attachment) =>
        deletedAttachments.Contains(attachment);

    /// <summary>The number of embedded file attachments in the document.</summary>
    public int AttachmentCount
    {
        get
        {
            lock (PdfiumNative.Sync)
            {
                var doc = Handle;
                return Math.Max(0, PdfiumNative.FPDFDoc_GetAttachmentCount(doc));
            }
        }
    }

    /// <summary>The embedded file attachments in the document.</summary>
    public IReadOnlyList<PdfAttachment> GetAttachments()
    {
        lock (PdfiumNative.Sync)
        {
            var doc = Handle;
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
                    attachments.Add(new(this, handle));
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
        var wideName = Interop.ToWideString(name);
        lock (PdfiumNative.Sync)
        {
            var doc = Handle;
            var handle = PdfiumNative.FPDFDoc_AddAttachment(doc, wideName);
            if (handle == IntPtr.Zero)
            {
                throw new PdfiumException($"Failed to add attachment '{name}' (duplicate name?)");
            }

            if (!PdfiumNative.FPDFAttachment_SetFile(handle, doc, contents, (uint) contents.Length))
            {
                // Do not leave an entry with no file behind in the name tree.
                RemoveAttachment(doc, handle);
                throw new PdfiumException($"Failed to write data for attachment '{name}'");
            }

            // In case PDFium reuses the address of an attachment deleted earlier.
            deletedAttachments.Remove(handle);
            return new(this, handle);
        }
    }

    // Caller must hold PdfiumNative.Sync. The name tree is ordered by name, so the index of
    // a just-added attachment is not known and has to be found by its handle.
    static void RemoveAttachment(IntPtr doc, IntPtr attachment)
    {
        var count = PdfiumNative.FPDFDoc_GetAttachmentCount(doc);
        for (var index = 0; index < count; index++)
        {
            if (PdfiumNative.FPDFDoc_GetAttachment(doc, index) == attachment)
            {
                PdfiumNative.FPDFDoc_DeleteAttachment(doc, index);
                return;
            }
        }
    }

    /// <summary>Removes the attachment at <paramref name="index"/> from the embedded files name tree.</summary>
    public void DeleteAttachment(int index)
    {
        lock (PdfiumNative.Sync)
        {
            var doc = Handle;
            var attachment = PdfiumNative.FPDFDoc_GetAttachment(doc, index);
            if (!PdfiumNative.FPDFDoc_DeleteAttachment(doc, index))
            {
                throw new PdfiumException($"Failed to delete attachment at index {index}");
            }

            deletedAttachments.Add(attachment);
        }
    }
}
