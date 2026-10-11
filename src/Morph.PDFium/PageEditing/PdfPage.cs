namespace Morph.PDFium;

public sealed partial class PdfPage
{
    /// <summary>
    /// The page's clockwise display rotation. Setting it changes how the page renders and
    /// reports its <see cref="Size"/>. Persist the change with <see cref="PdfiumDocument.Save(SaveFlags,int?)"/>.
    /// </summary>
    public PageRotation Rotation
    {
        get
        {
            lock (PdfiumNative.Sync)
            {
                var page = ValidHandle();
                return (PageRotation) PdfiumNative.FPDFPage_GetRotation(page);
            }
        }
        set
        {
            lock (PdfiumNative.Sync)
            {
                var page = ValidHandle();
                PdfiumNative.FPDFPage_SetRotation(page, (int) value);
            }
        }
    }

    /// <summary>The number of page objects (text, paths, images, ...) on the page.</summary>
    public int ObjectCount
    {
        get
        {
            lock (PdfiumNative.Sync)
            {
                var page = ValidHandle();
                return PdfiumNative.FPDFPage_CountObjects(page);
            }
        }
    }

    /// <summary>Whether the page contains any transparent content.</summary>
    public bool HasTransparency()
    {
        lock (PdfiumNative.Sync)
        {
            var page = ValidHandle();
            return PdfiumNative.FPDFPage_HasTransparency(page);
        }
    }

    /// <summary>
    /// Flattens annotations and form fields into the page content stream. Returns false when
    /// there was nothing to flatten. Persist the result with <see cref="PdfiumDocument.Save(SaveFlags,int?)"/>.
    /// </summary>
    public bool Flatten(bool forPrinting = false)
    {
        lock (PdfiumNative.Sync)
        {
            var page = ValidHandle();
            GeneratePendingContent();
            var result = PdfiumNative.FPDFPage_Flatten(page, forPrinting ? PdfiumNative.FlatPrint : PdfiumNative.FlatNormalDisplay);
            if (result == 0)
            {
                throw new PdfiumException($"Failed to flatten page {Index}");
            }

            // 1 == flattened, 2 == nothing to do.
            return result == 1;
        }
    }

    /// <summary>
    /// Regenerates the page content stream after edits. <see cref="AddText"/> and
    /// <see cref="AddRectangle"/> arrange this themselves, so calling it is only needed to
    /// force the regeneration early.
    /// </summary>
    public void GenerateContent()
    {
        lock (PdfiumNative.Sync)
        {
            var page = ValidHandle();
            if (!PdfiumNative.FPDFPage_GenerateContent(page))
            {
                throw new PdfiumException($"Failed to generate content for page {Index}");
            }

            contentPending = false;
        }
    }

    // Caller must hold PdfiumNative.Sync. Regenerates the content stream if an edit left it
    // out of date; a no-op otherwise.
    internal void GeneratePendingContent()
    {
        if (!contentPending)
        {
            return;
        }

        if (!PdfiumNative.FPDFPage_GenerateContent(handle))
        {
            throw new PdfiumException($"Failed to regenerate content for page {Index}");
        }

        contentPending = false;
    }
}
