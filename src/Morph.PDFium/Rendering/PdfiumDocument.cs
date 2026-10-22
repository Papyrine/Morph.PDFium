namespace Morph.PDFium;

public sealed partial class PdfiumDocument
{
    /// <summary>Renders a full page to a PNG using the supplied <paramref name="options"/>.</summary>
    public byte[] RenderPage(int index, RenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ThrowIfDisposed();
        ValidateIndex(index);
        options.Validate(nameof(options));

        using var rendered = RenderPixels(index, options.Dpi, options.ToFlags(), options.ToFillColor(), null);
        return PngEncoder.Encode(rendered.Pixels, rendered.Width, rendered.Height, options.Dpi, (int) options.CompressionLevel);
    }

    /// <summary>
    /// Renders every page to a PNG using the supplied <paramref name="options"/>, in parallel
    /// through <see cref="ProcessPages{T}(Func{PdfPage,T})"/>.
    /// </summary>
    public List<byte[]> RenderPages(RenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate(nameof(options));
        return [.. ProcessPages(_ => _.Render(options))];
    }

    /// <summary>
    /// Renders only the <paramref name="clip"/> rectangle of a page, scaled to
    /// <paramref name="options"/>.Dpi, to a PNG. Useful for tiles, thumbnails of a region, or
    /// zoomed views. The rectangle is in page coordinates, the same space that text, link
    /// and annotation rectangles are reported in, so one of those can be passed straight in.
    /// The image shows the area as the page displays it, so on a page rotated a quarter turn
    /// its width and height are swapped relative to the rectangle.
    /// </summary>
    public byte[] RenderRegion(int index, PdfRectangle clip, RenderOptions? options = null)
    {
        options ??= new();
        ThrowIfDisposed();
        ValidateIndex(index);
        options.Validate(nameof(options));
        ValidateClip(clip);

        using var rendered = RenderPixels(index, options.Dpi, options.ToFlags(), options.ToFillColor(), clip);
        return PngEncoder.Encode(rendered.Pixels, rendered.Width, rendered.Height, options.Dpi, (int) options.CompressionLevel);
    }

    internal static void ValidateClip(PdfRectangle clip)
    {
        // Written so that NaN, which fails every comparison, is rejected along with infinity.
        if (!double.IsFinite(clip.Width) || !double.IsFinite(clip.Height) || clip.Width <= 0 || clip.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(clip), clip, "clip must have positive, finite width and height");
        }
    }
}
