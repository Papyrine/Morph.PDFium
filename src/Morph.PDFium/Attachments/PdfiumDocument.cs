namespace Morph.PDFium;

public sealed partial class PdfiumDocument
{
    // The live attachments' handles, each with the token their PdfAttachments share. Deleting
    // an attachment marks its token deleted and drops it from here. PDFium may keep the object
    // readable after it leaves the name tree, or free it and hand its address to a later
    // attachment, so deletion is tracked on the token rather than the address: a deleted
    // attachment never answers for a new one at its address, and that new one gets a token of
    // its own, so is not taken for deleted.
    readonly Dictionary<IntPtr, AttachmentToken> attachmentTokens = [];

    // Caller must hold PdfiumNative.Sync.
    PdfAttachment WrapAttachment(IntPtr handle)
    {
        if (!attachmentTokens.TryGetValue(handle, out var token))
        {
            token = new();
            attachmentTokens[handle] = token;
        }

        return new(this, handle, token);
    }

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
                    attachments.Add(WrapAttachment(handle));
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

            return WrapAttachment(handle);
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

            if (attachmentTokens.Remove(attachment, out var token))
            {
                token.Deleted = true;
            }
        }
    }
}
