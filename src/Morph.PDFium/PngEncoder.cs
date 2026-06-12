/// <summary>
/// Minimal PNG writer (8 bit RGBA, Up filter, zlib) so the package carries no image
/// library dependency. A pHYs chunk records the render DPI so consumers display the
/// page at its physical size.
/// </summary>
static class PngEncoder
{
    static readonly byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    static readonly uint[] crcTable = BuildCrcTable();

    public static byte[] Encode(byte[] rgba, int width, int height, double dpi)
    {
        using var stream = new MemoryStream();
        stream.Write(signature);
        WriteChunk(stream, "IHDR"u8, BuildHeader(width, height));
        WriteChunk(stream, "pHYs"u8, BuildPhysicalDimensions(dpi));
        WriteChunk(stream, "IDAT"u8, Compress(rgba, width, height));
        WriteChunk(stream, "IEND"u8, []);
        return stream.ToArray();
    }

    static byte[] BuildHeader(int width, int height)
    {
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8; // bit depth
        header[9] = 6; // color type: truecolor with alpha
        // compression (0), filter (0) and interlace (0) bytes stay zero
        return header;
    }

    static byte[] BuildPhysicalDimensions(double dpi)
    {
        var chunk = new byte[9];
        var pixelsPerMeter = (uint) Math.Round(dpi / 0.0254);
        BinaryPrimitives.WriteUInt32BigEndian(chunk, pixelsPerMeter);
        BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(4), pixelsPerMeter);
        chunk[8] = 1; // unit: meter
        return chunk;
    }

    static byte[] Compress(byte[] rgba, int width, int height)
    {
        var stride = width * 4;
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            // The Up filter encodes each row as a delta from the row above. Page renders
            // are dominated by vertically repeating runs (white background, margins,
            // fills), which become zero runs once Up-filtered and deflate to near nothing.
            var previous = new byte[stride];
            var filtered = new byte[stride];
            for (var y = 0; y < height; y++)
            {
                var row = rgba.AsSpan(y * stride, stride);
                for (var x = 0; x < stride; x++)
                {
                    filtered[x] = (byte) (row[x] - previous[x]);
                }

                zlib.WriteByte(2); // filter: Up
                zlib.Write(filtered);
                row.CopyTo(previous);
            }
        }

        return output.ToArray();
    }

    static void WriteChunk(Stream stream, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, data.Length);
        stream.Write(buffer);
        stream.Write(type);
        stream.Write(data);

        var crc = UpdateCrc(uint.MaxValue, type);
        crc = UpdateCrc(crc, data);
        BinaryPrimitives.WriteUInt32BigEndian(buffer, crc ^ uint.MaxValue);
        stream.Write(buffer);
    }

    static uint UpdateCrc(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (var value in data)
        {
            crc = crcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return crc;
    }

    static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint index = 0; index < 256; index++)
        {
            var entry = index;
            for (var bit = 0; bit < 8; bit++)
            {
                entry = (entry & 1) == 1 ? 0xEDB88320 ^ (entry >> 1) : entry >> 1;
            }

            table[index] = entry;
        }

        return table;
    }
}
