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

    /// <summary>
    /// The zlib level the PNG is deflated at, 0 (stored) to 9 (smallest). Lower levels encode
    /// faster for a somewhat larger file: on text pages 4 is about a fifth faster than 6 for
    /// files a few percent larger. Defaults to <see cref="DefaultCompressionLevel"/> as it was
    /// when these options were created.
    /// </summary>
    public int CompressionLevel { get; init; } = DefaultCompressionLevel;

    static int defaultCompressionLevel = 6;

    /// <summary>
    /// The compression level for every render that is not given its own: those made with
    /// default <see cref="RenderOptions"/>, and the overloads that take only a dpi. 6 is zlib's
    /// default, what <see cref="System.IO.Compression.CompressionLevel.Optimal"/> gives.
    /// </summary>
    public static int DefaultCompressionLevel
    {
        get => defaultCompressionLevel;
        set
        {
            ValidateCompressionLevel(value, nameof(value));
            defaultCompressionLevel = value;
        }
    }

    // Shared by every render entry point. paramName is the caller's parameter: the options, or
    // a bare dpi.
    internal static void ValidateDpi(double dpi, string paramName)
    {
        if (!double.IsFinite(dpi) || dpi <= 0)
        {
            throw new ArgumentOutOfRangeException(paramName, dpi, "dpi must be positive and finite");
        }
    }

    static void ValidateCompressionLevel(int level, string paramName)
    {
        if (level is < 0 or > 9)
        {
            throw new ArgumentOutOfRangeException(paramName, level, "compression level must be between 0 and 9");
        }
    }

    internal void Validate(string paramName)
    {
        ValidateDpi(Dpi, paramName);
        ValidateCompressionLevel(CompressionLevel, paramName);
    }

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
