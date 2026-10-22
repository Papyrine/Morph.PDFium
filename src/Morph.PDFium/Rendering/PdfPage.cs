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
