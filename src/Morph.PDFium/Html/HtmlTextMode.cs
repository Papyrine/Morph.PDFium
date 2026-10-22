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
    /// Text drawn in its own color, weight and style, stretched to the width it has on the
    /// page. It uses the PDF font's family when the browser has that font, otherwise the
    /// nearest generic family; the PDF's embedded fonts are not carried over. Suits text-only HTML, with <see cref="HtmlOptions.IncludeImage"/> off.
    /// Text the PDF draws invisibly, such as an OCR layer over a scan, is left out.
    /// </summary>
    Visible
}
