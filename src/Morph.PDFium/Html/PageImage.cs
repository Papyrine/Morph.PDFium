namespace Morph.PDFium;

/// <summary>
/// A page's rendered pixels, as handed to <see cref="HtmlOptions.ImageUrlFromPixels"/>: what
/// the page's PNG would have held, before it is encoded. The pixels live in a buffer that is
/// reused once the call returns, so they must be read, encoded or copied during it, which is
/// why this is a ref struct.
/// </summary>
public readonly ref struct PageImage
{
    internal PageImage(ReadOnlySpan<byte> pixels, int width, int height, double dpi)
    {
        Pixels = pixels;
        Width = width;
        Height = height;
        Dpi = dpi;
    }

    /// <summary>
    /// The pixels, row by row from the top, each four bytes in the order red, green, blue and
    /// alpha, with straight (not premultiplied) alpha and no padding between rows: so
    /// <see cref="Width"/> × 4 bytes a row.
    /// </summary>
    public ReadOnlySpan<byte> Pixels { get; }

    /// <summary>The width in pixels.</summary>
    public int Width { get; }

    /// <summary>The height in pixels.</summary>
    public int Height { get; }

    /// <summary>The resolution the page was rendered at, in dots per inch.</summary>
    public double Dpi { get; }
}

/// <summary>
/// Given a page's zero-based index and its rendered pixels, returns the URL for the page's
/// image. See <see cref="HtmlOptions.ImageUrlFromPixels"/>.
/// </summary>
public delegate string PageImageUrl(int index, PageImage image);
