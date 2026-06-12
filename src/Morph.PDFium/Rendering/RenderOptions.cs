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
