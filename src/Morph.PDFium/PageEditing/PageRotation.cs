namespace Morph.PDFium;

/// <summary>A page's clockwise rotation, in 90-degree steps.</summary>
public enum PageRotation
{
    /// <summary>No rotation.</summary>
    None = 0,

    /// <summary>Rotated 90 degrees clockwise.</summary>
    Clockwise90 = 1,

    /// <summary>Rotated 180 degrees.</summary>
    Clockwise180 = 2,

    /// <summary>Rotated 270 degrees clockwise (90 counter-clockwise).</summary>
    Clockwise270 = 3
}
