namespace Morph.PDFium;

/// <summary>A page's clockwise rotation, in 90-degree steps.</summary>
public enum PageRotation
{
    /// <summary>No rotation.</summary>
    None = 0,

    /// <summary>Rotated 90 degrees clockwise.</summary>
    Clockwise90 = 1,

    /// <summary>Rotated 180 degrees.</summary>
    Clockwise180 = 2,

    /// <summary>Rotated 270 degrees clockwise (90 counter-clockwise).</summary>
    Clockwise270 = 3
}

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
            var page = ValidHandle();
            lock (PdfiumNative.Sync)
            {
                return (PageRotation) PdfiumNative.FPDFPage_GetRotation(page);
            }
        }
        set
        {
            var page = ValidHandle();
            lock (PdfiumNative.Sync)
            {
                PdfiumNative.FPDFPage_SetRotation(page, (int) value);
            }
        }
    }

    /// <summary>The number of page objects (text, paths, images, ...) on the page.</summary>
    public int ObjectCount
    {
        get
        {
            var page = ValidHandle();
            lock (PdfiumNative.Sync)
            {
                return PdfiumNative.FPDFPage_CountObjects(page);
            }
        }
    }

    /// <summary>Whether the page contains any transparent content.</summary>
    public bool HasTransparency()
    {
        var page = ValidHandle();
        lock (PdfiumNative.Sync)
        {
            return PdfiumNative.FPDFPage_HasTransparency(page);
        }
    }

    /// <summary>
    /// Flattens annotations and form fields into the page content stream. Returns false when
    /// there was nothing to flatten. Persist the result with <see cref="PdfiumDocument.Save(SaveFlags,int?)"/>.
    /// </summary>
    public bool Flatten(bool forPrinting = false)
    {
        var page = ValidHandle();
        lock (PdfiumNative.Sync)
        {
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
    /// Regenerates the page content stream after edits. Call before saving when page objects
    /// have been added, removed or modified.
    /// </summary>
    public void GenerateContent()
    {
        var page = ValidHandle();
        lock (PdfiumNative.Sync)
        {
            if (!PdfiumNative.FPDFPage_GenerateContent(page))
            {
                throw new PdfiumException($"Failed to generate content for page {Index}");
            }
        }
    }
}
