namespace Morph.PDFium;

public sealed partial class PdfPage
{
    /// <summary>
    /// Renders this page to a PNG, as <see cref="PdfiumDocument.RenderPage(int, double)"/>
    /// does. The page is already loaded, so this skips parsing it again, which makes it the
    /// cheaper choice when the page is open anyway, for its text, say.
    /// </summary>
    public byte[] Render(double dpi = 96)
    {
        RenderOptions.ValidateDpi(dpi, nameof(dpi));
        using var rendered = RenderPixels(dpi, PdfiumNative.RenderAnnotations, PdfiumDocument.OpaqueWhite, null);
        return PngEncoder.Encode(rendered.Pixels, rendered.Width, rendered.Height, dpi, (int) RenderOptions.DefaultCompressionLevel);
    }

    /// <summary>Renders this page to a PNG using the supplied <paramref name="options"/>.</summary>
    public byte[] Render(RenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate(nameof(options));
        using var rendered = RenderPixels(options.Dpi, options.ToFlags(), options.ToFillColor(), null);
        return PngEncoder.Encode(rendered.Pixels, rendered.Width, rendered.Height, options.Dpi, (int) options.CompressionLevel);
    }

    /// <summary>
    /// Renders only the <paramref name="clip"/> rectangle of this page to a PNG, as
    /// <see cref="PdfiumDocument.RenderRegion"/> does.
    /// </summary>
    public byte[] RenderRegion(PdfRectangle clip, RenderOptions? options = null)
    {
        options ??= new();
        options.Validate(nameof(options));
        PdfiumDocument.ValidateClip(clip);
        using var rendered = RenderPixels(options.Dpi, options.ToFlags(), options.ToFillColor(), clip);
        return PngEncoder.Encode(rendered.Pixels, rendered.Width, rendered.Height, options.Dpi, (int) options.CompressionLevel);
    }

    // Form XObjects nest; malformed ones could nest without end.
    const int maxFormDepth = 32;

    /// <summary>
    /// Renders this page to a PNG as <see cref="Render(RenderOptions)"/> does, but with the
    /// page's text left out: its images, drawings and annotations only. Each text object is
    /// switched to draw nothing for the render and switched back straight after, all under the
    /// PDFium lock, so nothing else sees the change and the page's content is never written
    /// out with it. Text that also clips keeps clipping, so what it clips still looks the same.
    /// </summary>
    internal byte[] RenderWithoutText(RenderOptions options)
    {
        RenderedPixels rendered;
        lock (PdfiumNative.Sync)
        {
            var page = ValidHandle();
            // Written out before the text is hidden, never while it is.
            document.GeneratePendingContent();
            var hidden = new List<(IntPtr Object, int Mode)>();
            try
            {
                var count = PdfiumNative.FPDFPage_CountObjects(page);
                for (var index = 0; index < count; index++)
                {
                    HideText(PdfiumNative.FPDFPage_GetObject(page, index), hidden, 0);
                }

                var size = new PdfiumNative.PageSizeF
                {
                    Width = PdfiumNative.FPDF_GetPageWidthF(page),
                    Height = PdfiumNative.FPDF_GetPageHeightF(page)
                };
                rendered = PdfiumDocument.RenderLoadedPage(page, size, Index, options.Dpi, options.ToFlags() | PdfiumNative.ReverseByteOrder, options.ToFillColor(), null, IntPtr.Zero);
            }
            finally
            {
                foreach (var (textObject, mode) in hidden)
                {
                    PdfiumNative.FPDFTextObj_SetTextRenderMode(textObject, mode);
                }
            }
        }

        using (rendered)
        {
            return PngEncoder.Encode(rendered.Pixels, rendered.Width, rendered.Height, options.Dpi, (int) options.CompressionLevel);
        }
    }

    // Caller must hold PdfiumNative.Sync. Switches a text object, or the text objects within a
    // form, to draw nothing, recording each one's mode to restore. Modes 4 to 7 draw as 0 to 3
    // do and also add the text to the clipping path, so those become 7, which only clips.
    static void HideText(IntPtr pageObject, List<(IntPtr Object, int Mode)> hidden, int depth)
    {
        if (pageObject == IntPtr.Zero)
        {
            return;
        }

        switch ((PdfPageObjectType) PdfiumNative.FPDFPageObj_GetType(pageObject))
        {
            case PdfPageObjectType.Text:
                // An unknown mode (-1) is drawn filled.
                var mode = Math.Max(PdfiumNative.TextRenderModeFill, PdfiumNative.FPDFTextObj_GetTextRenderMode(pageObject));
                if (mode % 4 == PdfiumNative.TextRenderModeInvisible)
                {
                    return;
                }

                var invisible = mode >= 4 ? PdfiumNative.TextRenderModeInvisible + 4 : PdfiumNative.TextRenderModeInvisible;
                if (PdfiumNative.FPDFTextObj_SetTextRenderMode(pageObject, invisible))
                {
                    hidden.Add((pageObject, mode));
                }

                break;
            case PdfPageObjectType.Form when depth < maxFormDepth:
                var count = PdfiumNative.FPDFFormObj_CountObjects(pageObject);
                for (var index = 0; index < count; index++)
                {
                    HideText(PdfiumNative.FPDFFormObj_GetObject(pageObject, (uint) index), hidden, depth + 1);
                }

                break;
        }
    }

    // The counterpart of PdfiumDocument.RenderPixels for a page that is already loaded.
    RenderedPixels RenderPixels(double dpi, int renderFlags, uint background, PdfRectangle? clip)
    {
        lock (PdfiumNative.Sync)
        {
            var page = ValidHandle();
            document.GeneratePendingContent();
            var size = new PdfiumNative.PageSizeF
            {
                Width = PdfiumNative.FPDF_GetPageWidthF(page),
                Height = PdfiumNative.FPDF_GetPageHeightF(page)
            };
            return PdfiumDocument.RenderLoadedPage(page, size, Index, dpi, renderFlags | PdfiumNative.ReverseByteOrder, background, clip, IntPtr.Zero);
        }
    }
}
