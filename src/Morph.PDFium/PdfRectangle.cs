namespace Morph.PDFium;

/// <summary>
/// A rectangle in PDF "user space" (points; 1/72 inch). The origin is the bottom-left
/// of the page, so <see cref="Top"/> is greater than <see cref="Bottom"/> and
/// <see cref="Right"/> is greater than <see cref="Left"/> for a normalised rectangle.
/// </summary>
public readonly record struct PdfRectangle(double Left, double Bottom, double Right, double Top)
{
    /// <summary>Width of the rectangle in points.</summary>
    public double Width => Right - Left;

    /// <summary>Height of the rectangle in points.</summary>
    public double Height => Top - Bottom;
}

/// <summary>A point in PDF "user space" (points; origin at the bottom-left of the page).</summary>
public readonly record struct PdfPoint(double X, double Y);
