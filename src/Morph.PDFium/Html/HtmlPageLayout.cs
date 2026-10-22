using Morph.PDFium;

/// <summary>
/// What a page contributes to its HTML, read under the PDFium lock and written out after it is
/// released. Everything is in display coordinates: points from the top-left corner of the page
/// as it is shown, after its rotation and with its crop box origin removed.
/// </summary>
sealed record HtmlPageLayout(int Index, double Width, double Height, IReadOnlyList<HtmlTextRun> Runs, IReadOnlyList<HtmlLink> Links);

/// <summary>
/// A stretch of text drawn along one baseline in one font, size and style. (X, Y) is where the
/// baseline starts, Angle the clockwise rotation in degrees and Length how far the text reaches
/// along the baseline, which the browser stretches its own glyphs to fill.
/// </summary>
sealed record HtmlTextRun(string Text, double X, double Y, double Angle, double FontSize, double Length, HtmlTextStyle Style);

/// <summary>
/// The look of visible text. Family is the PDF font's family name, when it has one; Generic is
/// the CSS generic family to fall back to. The default value stands for overlay text, which has
/// no look of its own.
/// </summary>
readonly record struct HtmlTextStyle(string? Family, string? Generic, int Weight, bool Italic, PdfColor Fill);

sealed record HtmlLink(string Href, IReadOnlyList<HtmlRect> Rectangles);

readonly record struct HtmlRect(double X, double Y, double Width, double Height);

/// <summary>The affine map from page coordinates to display coordinates.</summary>
readonly record struct DisplayMatrix(double A, double B, double C, double D, double E, double F)
{
    public (double X, double Y) Map(double x, double y) =>
        (A * x + C * y + E, B * x + D * y + F);

    public (double X, double Y) MapVector(double x, double y) =>
        (A * x + C * y, B * x + D * y);

    // Page rotations are quarter turns, so a rectangle maps to a rectangle.
    public HtmlRect Map(PdfRectangle rectangle)
    {
        var (x1, y1) = Map(rectangle.Left, rectangle.Bottom);
        var (x2, y2) = Map(rectangle.Right, rectangle.Top);
        return new(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1));
    }
}
