namespace Morph.PDFium;

/// <summary>
/// Options for the advanced render overloads. The defaults match the simple
/// <see cref="PdfiumDocument.RenderPage(int,double)"/>: 96 DPI, white background, annotations
/// drawn and anti-aliasing on.
/// </summary>
public sealed record RenderOptions
{
    /// <summary>Output resolution in dots per inch. Must be positive.</summary>
    public double Dpi { get; init; } = 96;

    /// <summary>Whether to render annotations (excluding interactive widgets and popups).</summary>
    public bool RenderAnnotations { get; init; } = true;

    /// <summary>Render in grayscale.</summary>
    public bool Grayscale { get; init; }

    /// <summary>Optimize for printing rather than screen display.</summary>
    public bool ForPrinting { get; init; }

    /// <summary>Disable text anti-aliasing.</summary>
    public bool DisableTextSmoothing { get; init; }

    /// <summary>Disable image anti-aliasing.</summary>
    public bool DisableImageSmoothing { get; init; }

    /// <summary>Disable path anti-aliasing.</summary>
    public bool DisablePathSmoothing { get; init; }

    /// <summary>The background color filled before rendering. Defaults to opaque white.</summary>
    public PdfColor Background { get; init; } = new(255, 255, 255, 255);

    internal int ToFlags()
    {
        var flags = 0;
        if (RenderAnnotations)
        {
            flags |= PdfiumNative.RenderAnnotations;
        }

        if (Grayscale)
        {
            flags |= PdfiumNative.RenderGrayscale;
        }

        if (ForPrinting)
        {
            flags |= PdfiumNative.RenderForPrinting;
        }

        if (DisableTextSmoothing)
        {
            flags |= PdfiumNative.RenderNoSmoothText;
        }

        if (DisableImageSmoothing)
        {
            flags |= PdfiumNative.RenderNoSmoothImage;
        }

        if (DisablePathSmoothing)
        {
            flags |= PdfiumNative.RenderNoSmoothPath;
        }

        return flags;
    }

    // FPDFBitmap_FillRect writes BGRA bytes, but the page render uses ReverseByteOrder so the
    // buffer is read back as RGBA. Swap R and B in the fill word so both agree in memory.
    internal uint ToFillColor() =>
        ((uint) Background.A << 24) | ((uint) Background.B << 16) | ((uint) Background.G << 8) | Background.R;
}

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

        var (pixels, width, height) = RenderPixels(index, options.Dpi, options.ToFlags(), null);
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
        var region = new ClipRegion(clip, width, height, options.ToFillColor());
        var (pixels, w, h) = RenderPixels(index, options.Dpi, options.ToFlags(), region);
        return PngEncoder.Encode(pixels, w, h, options.Dpi);
    }
}
