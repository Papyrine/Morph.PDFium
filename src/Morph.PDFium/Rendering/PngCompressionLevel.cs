namespace Morph.PDFium;

/// <summary>
/// The zlib level a rendered PNG is deflated at; each value's number is its zlib level. Lower
/// levels encode faster for a somewhat larger file. Set per render through
/// <see cref="RenderOptions.CompressionLevel"/>, or for every render that is not given its own
/// through <see cref="RenderOptions.DefaultCompressionLevel"/>.
/// </summary>
public enum PngCompressionLevel
{
    /// <summary>Stored rather than deflated: the largest file, and the quickest to write.</summary>
    NoCompression = 0,
    Fastest = 1,
    Level2 = 2,
    Level3 = 3,
    /// <summary>
    /// On text pages about a fifth faster than <see cref="Optimal"/> for files a few percent larger.
    /// </summary>
    Level4 = 4,
    Level5 = 5,
    /// <summary>
    /// zlib's default level, what <see cref="System.IO.Compression.CompressionLevel.Optimal"/>
    /// gives. The default.
    /// </summary>
    Optimal = 6,
    Level7 = 7,
    Level8 = 8,
    /// <summary>The smallest file, and the slowest to write.</summary>
    SmallestSize = 9
}
