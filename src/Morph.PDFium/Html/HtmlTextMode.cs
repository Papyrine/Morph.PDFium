namespace Morph.PDFium;

/// <summary>How <see cref="HtmlOptions"/> puts a page's text into the HTML.</summary>
public enum HtmlTextMode
{
    /// <summary>No text: the page is only its image (and links).</summary>
    None,

    /// <summary>
    /// Transparent text laid exactly over the page image, so the page looks like the image
    /// but its text can be selected, copied and found with the browser's search, as in a PDF
    /// viewer. The default. Without the image this text is invisible; use
    /// <see cref="Visible"/> for text-only HTML.
    /// </summary>
    Overlay,

    /// <summary>
    /// Text drawn in its own color, weight and style, filled, outlined or both as the PDF draws
    /// it, stretched to the width it has on the page. It uses the PDF font's family when the
    /// browser has that font, otherwise the nearest generic family; the PDF's embedded fonts
    /// are not carried over. Text the PDF draws invisibly, such as an OCR layer over a scan, is
    /// left out.
    /// <para>
    /// With <see cref="HtmlOptions.IncludeImage"/> on, the page image is rendered without its
    /// text, so the page keeps its images and drawings while its text stays sharp at any zoom.
    /// The text is in the browser's fonts, so it looks less like the PDF than
    /// <see cref="Overlay"/> does. With the image off, the HTML is the text alone.
    /// </para>
    /// </summary>
    Visible
}
