namespace Morph.PDFium;

public sealed partial class PdfiumDocument
{
    /// <summary>Renders a full page to a PNG using the supplied <paramref name="options"/>.</summary>
    public byte[] RenderPage(int index, RenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ThrowIfDisposed();
        ValidateIndex(index);
        if (options.Dpi <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.Dpi, "dpi must be positive");
        }

        var (pixels, width, height) = RenderPixels(index, options.Dpi, options.ToFlags(), options.ToFillColor(), null);
        return PngEncoder.Encode(pixels, width, height, options.Dpi);
    }

    /// <summary>
    /// Renders only the <paramref name="clip"/> rectangle of a page (page points), scaled to
    /// <paramref name="options"/>.Dpi, to a PNG. Useful for tiles, thumbnails of a region, or
    /// zoomed views.
    /// </summary>
    public byte[] RenderRegion(int index, PdfRectangle clip, RenderOptions? options = null)
    {
        options ??= new();
        ThrowIfDisposed();
        ValidateIndex(index);
        if (options.Dpi <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.Dpi, "dpi must be positive");
        }

        if (clip.Width <= 0 || clip.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(clip), clip, "clip must have positive width and height");
        }

        var width = Math.Max(1, (int) Math.Round(clip.Width * options.Dpi / 72d));
        var height = Math.Max(1, (int) Math.Round(clip.Height * options.Dpi / 72d));
        var region = new ClipRegion(clip, width, height);
        var (pixels, w, h) = RenderPixels(index, options.Dpi, options.ToFlags(), options.ToFillColor(), region);
        return PngEncoder.Encode(pixels, w, h, options.Dpi);
    }
}
