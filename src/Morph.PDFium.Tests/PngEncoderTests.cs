using System.IO.Compression;
using System.Text;

// Exercises the encoder on synthetic images: the format it picks for each, and that the pixels
// survive a round trip through the zlib stream it writes.
public class PngEncoderTests
{
    const int width = 200;
    const int height = 300;

    [Test]
    public async Task RepeatingGrayRowsUseGrayscaleAndNoFilter()
    {
        var rgba = RepeatingRows();
        var png = PngEncoder.Encode(rgba, width, height, 96, 6);
        var (colorType, filters, pixels) = Decode(png, width, height);

        await Assert.That(colorType).IsEqualTo((byte) 0);
        await Assert.That(filters.Distinct().Single()).IsEqualTo((byte) 0);
        await Assert.That(pixels).IsEquivalentTo(Reds(rgba));
    }

    [Test]
    public async Task DriftingGrayRowsUseGrayscaleAndUpFilter()
    {
        var rgba = DriftingRows(color: false);
        var png = PngEncoder.Encode(rgba, width, height, 96, 6);
        var (colorType, filters, pixels) = Decode(png, width, height);

        await Assert.That(colorType).IsEqualTo((byte) 0);
        await Assert.That(filters.Distinct().Single()).IsEqualTo((byte) 2);
        await Assert.That(pixels).IsEquivalentTo(Reds(rgba));
    }

    [Test]
    public async Task ColorImageKeepsEveryChannel()
    {
        var rgba = DriftingRows(color: true);
        var png = PngEncoder.Encode(rgba, width, height, 96, 6);
        var (colorType, filters, pixels) = Decode(png, width, height);

        await Assert.That(colorType).IsEqualTo((byte) 6);
        await Assert.That(filters.Distinct().Single()).IsEqualTo((byte) 2);
        await Assert.That(pixels).IsEquivalentTo(rgba);
    }

    [Test]
    public async Task TranslucentGrayImageKeepsAlpha()
    {
        var rgba = RepeatingRows();
        for (var index = 3; index < rgba.Length; index += 4)
        {
            rgba[index] = 128;
        }

        var png = PngEncoder.Encode(rgba, width, height, 96, 6);
        var (colorType, _, pixels) = Decode(png, width, height);

        await Assert.That(colorType).IsEqualTo((byte) 6);
        await Assert.That(pixels).IsEquivalentTo(rgba);
    }

    [Test]
    public async Task EveryCompressionLevelRoundTrips()
    {
        var rgba = DriftingRows(color: true);
        var sizes = new List<int>();
        foreach (var level in new[] { 0, 1, 4, 9 })
        {
            var png = PngEncoder.Encode(rgba, width, height, 96, level);
            var (_, _, pixels) = Decode(png, width, height);
            await Assert.That(pixels).IsEquivalentTo(rgba);
            sizes.Add(png.Length);
        }

        // Stored (level 0) is larger than the raw image; higher levels never come out larger.
        await Assert.That(sizes[0]).IsGreaterThan(rgba.Length);
        await Assert.That(sizes[1]).IsLessThan(sizes[0]);
        await Assert.That(sizes[2]).IsLessThanOrEqualTo(sizes[1]);
        await Assert.That(sizes[3]).IsLessThanOrEqualTo(sizes[2]);
    }

    // One colour pixel right at the end means the gray plane is built in full and then found
    // unusable, which must still leave the image encoded as RGBA.
    [Test]
    public async Task GrayExceptLastPixelKeepsRgba()
    {
        var rgba = RepeatingRows();
        rgba[^4] = 255;
        rgba[^3] = 0;
        rgba[^2] = 0;
        var png = PngEncoder.Encode(rgba, width, height, 96, 6);
        var (colorType, _, pixels) = Decode(png, width, height);

        await Assert.That(colorType).IsEqualTo((byte) 6);
        await Assert.That(pixels).IsEquivalentTo(rgba);
    }

    // A pixel count that is not a multiple of any vector width, so the scalar tail of the gray
    // extraction runs as well as its vector blocks.
    [Test]
    public async Task OddSizedGrayImageRoundTrips()
    {
        const int oddWidth = 203;
        const int oddHeight = 37;
        var random = new Random(3);
        var rgba = new byte[oddWidth * oddHeight * 4];
        for (var pixel = 0; pixel < rgba.Length; pixel += 4)
        {
            var level = (byte) random.Next(256);
            rgba[pixel] = level;
            rgba[pixel + 1] = level;
            rgba[pixel + 2] = level;
            rgba[pixel + 3] = 255;
        }

        var png = PngEncoder.Encode(rgba, oddWidth, oddHeight, 96, 6);
        var (colorType, _, pixels) = Decode(png, oddWidth, oddHeight);

        await Assert.That(colorType).IsEqualTo((byte) 0);
        await Assert.That(pixels).IsEquivalentTo(Reds(rgba));
    }

    // The encoder's table driven CRC must agree with the bit by bit definition on every chunk,
    // whose lengths exercise both its eight byte steps and its tail.
    [Test]
    public async Task ChunkCrcsAreCorrect()
    {
        foreach (var rgba in new[] { RepeatingRows(), DriftingRows(color: true) })
        {
            var png = PngEncoder.Encode(rgba, width, height, 96, 6);
            var offset = 8;
            while (offset < png.Length)
            {
                var length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset));
                // The CRC covers the chunk type and data but not the length.
                var expected = ReferenceCrc(png.AsSpan(offset + 4, 4 + length));
                var stored = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(offset + 8 + length));
                await Assert.That(stored).IsEqualTo(expected);
                offset += 12 + length;
            }
        }
    }

    static uint ReferenceCrc(ReadOnlySpan<byte> data)
    {
        var crc = uint.MaxValue;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) == 1 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1;
            }
        }

        return crc ^ uint.MaxValue;
    }

    // Gray rows that repeat exactly two rows back, as the glyph shapes of a text page recur
    // down it, but differ sharply from the row directly above: unfiltered they deflate to
    // almost nothing, Up filtered they are noise.
    static byte[] RepeatingRows()
    {
        var random = new Random(1);
        var even = new byte[width];
        var odd = new byte[width];
        random.NextBytes(even);
        random.NextBytes(odd);
        var rgba = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            var source = y % 2 == 0 ? even : odd;
            for (var x = 0; x < width; x++)
            {
                var pixel = (y * width + x) * 4;
                rgba[pixel] = source[x];
                rgba[pixel + 1] = source[x];
                rgba[pixel + 2] = source[x];
                rgba[pixel + 3] = 255;
            }
        }

        return rgba;
    }

    // Photograph-like: noise in which each row drifts a little from the one above, so the Up
    // filter leaves small values where the rows themselves are incompressible.
    static byte[] DriftingRows(bool color)
    {
        var random = new Random(2);
        var rgba = new byte[width * height * 4];
        var previous = new byte[width * 3];
        random.NextBytes(previous);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pixel = (y * width + x) * 4;
                for (var channel = 0; channel < 3; channel++)
                {
                    // A gray pixel drifts once and uses that level for all three channels.
                    var source = x * 3 + channel;
                    if (color || channel == 0)
                    {
                        previous[source] = (byte) Math.Clamp(previous[source] + random.Next(-3, 4), 0, 255);
                    }

                    rgba[pixel + channel] = previous[color ? source : x * 3];
                }

                rgba[pixel + 3] = 255;
            }
        }

        return rgba;
    }

    static byte[] Reds(byte[] rgba) =>
        rgba.Where((_, index) => index % 4 == 0).ToArray();

    // Reads the color type and, by inflating the image data and undoing the filters, the
    // scanlines the file holds.
    static (byte colorType, List<byte> filters, byte[] pixels) Decode(byte[] png, int width, int height)
    {
        byte colorType = 0;
        using var compressed = new MemoryStream();
        var offset = 8;
        while (offset < png.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset));
            var type = Encoding.ASCII.GetString(png, offset + 4, 4);
            var data = png.AsSpan(offset + 8, length);
            if (type == "IHDR")
            {
                colorType = data[9];
            }

            if (type == "IDAT")
            {
                compressed.Write(data);
            }

            offset += 12 + length;
        }

        compressed.Position = 0;
        using var inflated = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionMode.Decompress))
        {
            zlib.CopyTo(inflated);
        }

        var raw = inflated.ToArray();
        var stride = width * (colorType == 0 ? 1 : 4);
        var pixels = new byte[stride * height];
        var filters = new List<byte>();
        for (var y = 0; y < height; y++)
        {
            var filter = raw[y * (stride + 1)];
            filters.Add(filter);
            var row = raw.AsSpan(y * (stride + 1) + 1, stride);
            var target = pixels.AsSpan(y * stride, stride);
            var previous = y == 0 ? new byte[stride] : pixels.AsSpan((y - 1) * stride, stride);
            for (var x = 0; x < stride; x++)
            {
                target[x] = filter == 2 ? (byte) (row[x] + previous[x]) : row[x];
            }
        }

        return (colorType, filters, pixels);
    }
}
