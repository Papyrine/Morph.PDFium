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
    /// page's text left out: its images, drawings and annotations only. Text that also clips
    /// keeps clipping, so what it clips still looks the same.
    /// </summary>
    /// <remarks>
    /// Switching a text object's render mode marks it changed in PDFium, even when it is
    /// switched back, and the next content regeneration (an edit, or GenerateContent) would
    /// then rewrite the page's original content streams. So by default the text is hidden on a
    /// second, throwaway load of the page, closed without ever generating content, and this
    /// page is untouched. <paramref name="inPlace"/> hides it on this page instead, saving
    /// the second parse, which is safe only when the caller is about to dispose this page,
    /// unedited, and uses nothing else of it in between: the library's own pages, loaded to
    /// convert and disposed straight after.
    /// </remarks>
    internal byte[] RenderWithoutText(RenderOptions options, bool inPlace)
    {
        RenderedPixels rendered;
        lock (PdfiumNative.Sync)
        {
            var current = ValidHandle();
            // Written out first: the throwaway page is loaded from the document's content, and
            // this page must have nothing left to write when it is disposed with its text hidden.
            document.GeneratePendingContent();
            var page = current;
            if (!inPlace)
            {
                page = PdfiumNative.FPDF_LoadPage(DocumentHandle, Index);
                if (page == IntPtr.Zero)
                {
                    throw new PdfiumException($"Failed to load page {Index}");
                }
            }

            try
            {
                var count = PdfiumNative.FPDFPage_CountObjects(page);
                for (var index = 0; index < count; index++)
                {
                    HideText(PdfiumNative.FPDFPage_GetObject(page, index), 0);
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
                if (!inPlace)
                {
                    PdfiumNative.FPDF_ClosePage(page);
                }
            }
        }

        using (rendered)
        {
            return PngEncoder.Encode(rendered.Pixels, rendered.Width, rendered.Height, options.Dpi, (int) options.CompressionLevel);
        }
    }

    // Caller must hold PdfiumNative.Sync. Switches a text object, or the text objects within a
    // form, to draw nothing. Modes 4 to 7 draw as 0 to 3 do and also add the text to the
    // clipping path, so those become 7, which only clips.
    static void HideText(IntPtr pageObject, int depth)
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
                if (mode % 4 != PdfiumNative.TextRenderModeInvisible)
                {
                    PdfiumNative.FPDFTextObj_SetTextRenderMode(pageObject, mode >= 4 ? PdfiumNative.TextRenderModeInvisible + 4 : PdfiumNative.TextRenderModeInvisible);
                }

                break;
            case PdfPageObjectType.Form when depth < maxFormDepth:
                var count = PdfiumNative.FPDFFormObj_CountObjects(pageObject);
                for (var index = 0; index < count; index++)
                {
                    HideText(PdfiumNative.FPDFFormObj_GetObject(pageObject, (uint) index), depth + 1);
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
