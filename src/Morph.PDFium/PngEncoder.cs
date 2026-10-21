/// <summary>
/// Minimal PNG writer (8 bit RGBA or grayscale, Up filter, zlib) so the package carries no
/// image library dependency. A pHYs chunk records the render DPI so consumers display the
/// page at its physical size.
/// </summary>
static class PngEncoder
{
    static readonly byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    static readonly uint[] crcTable = BuildCrcTable();

    public static byte[] Encode(byte[] rgba, int width, int height, double dpi)
    {
        // Most pages are dark text on a white background, which anti-aliases to shades of
        // gray. Such a page loses nothing as a one byte per pixel grayscale image, and it
        // gives deflate a quarter of the data, so it encodes faster and to a smaller file.
        var gray = IsOpaqueGray(rgba);
        using var stream = new MemoryStream();
        stream.Write(signature);
        WriteChunk(stream, "IHDR"u8, BuildHeader(width, height, gray));
        WriteChunk(stream, "pHYs"u8, BuildPhysicalDimensions(dpi));
        WriteImageData(stream, rgba, width, height, gray);
        WriteChunk(stream, "IEND"u8, []);
        return stream.ToArray();
    }

    static byte[] BuildHeader(int width, int height, bool gray)
    {
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8; // bit depth
        // color type: grayscale (0) or truecolor with alpha (6)
        header[9] = gray ? (byte) 0 : (byte) 6;
        // compression (0), filter (0) and interlace (0) bytes stay zero
        return header;
    }

    // Whether every pixel is opaque with equal red, green and blue. Read as a little endian
    // word an RGBA pixel is R | G << 8 | B << 16 | A << 24, so it qualifies when it equals its
    // red byte copied into the other two color bytes with the alpha byte set.
    static bool IsOpaqueGray(ReadOnlySpan<byte> rgba)
    {
        if (!BitConverter.IsLittleEndian)
        {
            return false;
        }

        var pixels = MemoryMarshal.Cast<byte, uint>(rgba);
        var index = 0;
        if (Vector.IsHardwareAccelerated)
        {
            var red = new Vector<uint>(0xFF);
            var spread = new Vector<uint>(0x010101);
            var opaque = new Vector<uint>(0xFF000000);
            for (; index <= pixels.Length - Vector<uint>.Count; index += Vector<uint>.Count)
            {
                var pixel = new Vector<uint>(pixels[index..]);
                if (pixel != (((pixel & red) * spread) | opaque))
                {
                    return false;
                }
            }
        }

        for (; index < pixels.Length; index++)
        {
            var pixel = pixels[index];
            if (pixel != (((pixel & 0xFF) * 0x010101) | 0xFF000000))
            {
                return false;
            }
        }

        return true;
    }

    static byte[] BuildPhysicalDimensions(double dpi)
    {
        var chunk = new byte[9];
        // PNG limits its four byte integers to 31 bits, and decoders reject a larger value.
        var pixelsPerMeter = (uint) Math.Min(Math.Round(dpi / 0.0254), int.MaxValue);
        BinaryPrimitives.WriteUInt32BigEndian(chunk, pixelsPerMeter);
        BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(4), pixelsPerMeter);
        chunk[8] = 1; // unit: meter
        return chunk;
    }

    // Writes the IDAT chunk. The compressed data goes straight into the output, and the chunk
    // length and CRC, which are only known once it is all written, are filled in afterwards.
    // That avoids compressing into a buffer of its own and copying the result across.
    static void WriteImageData(MemoryStream stream, byte[] rgba, int width, int height, bool gray)
    {
        var start = (int) stream.Position;
        Span<byte> word = stackalloc byte[4];
        stream.Write(word); // length, patched below
        stream.Write("IDAT"u8);
        Compress(stream, rgba, width, height, gray);

        var chunk = stream.GetBuffer().AsSpan(start, (int) stream.Position - start);
        BinaryPrimitives.WriteInt32BigEndian(chunk, chunk.Length - 8);
        // The CRC covers the chunk type and data but not the length.
        var crc = UpdateCrc(uint.MaxValue, chunk[4..]);
        BinaryPrimitives.WriteUInt32BigEndian(word, crc ^ uint.MaxValue);
        stream.Write(word);
    }

    static void Compress(Stream output, byte[] rgba, int width, int height, bool gray)
    {
        var sourceStride = width * 4;
        var stride = sourceStride;
        if (gray)
        {
            stride = width;
        }

        using var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true);
        // The Up filter encodes each row as a delta from the row above. Page renders
        // are dominated by vertically repeating runs (white background, margins,
        // fills), which become zero runs once Up-filtered and deflate to near nothing.
        // The row above the first is defined as all zeros.
        Span<byte> previous = new byte[stride];
        // A grayscale row is extracted into one of two buffers that take turns holding the
        // current row and the one above it. An RGBA row is filtered straight from the source.
        Span<byte> spare = gray ? new byte[stride] : [];
        // Each filtered row is preceded by its filter type byte, so a row goes to zlib in one write.
        var filtered = new byte[stride + 1];
        filtered[0] = 2; // filter: Up
        for (var y = 0; y < height; y++)
        {
            var row = rgba.AsSpan(y * sourceStride, sourceStride);
            if (gray)
            {
                ExtractRed(row, spare);
                row = spare;
                spare = previous;
            }

            Subtract(row, previous, filtered.AsSpan(1));
            zlib.Write(filtered);
            previous = row;
        }
    }

    // Copies the red byte of each RGBA pixel, which on a gray pixel is its level, to destination.
    static void ExtractRed(ReadOnlySpan<byte> rgba, Span<byte> destination)
    {
        var pixels = MemoryMarshal.Cast<byte, uint>(rgba);
        var index = 0;
        if (Vector.IsHardwareAccelerated)
        {
            // Each narrowing keeps the low half of every element, so two of them reduce four
            // vectors of little endian pixels to one vector of their red bytes.
            var count = Vector<uint>.Count;
            for (; index <= pixels.Length - count * 4; index += count * 4)
            {
                var low = Vector.Narrow(new Vector<uint>(pixels[index..]), new Vector<uint>(pixels[(index + count)..]));
                var high = Vector.Narrow(new Vector<uint>(pixels[(index + count * 2)..]), new Vector<uint>(pixels[(index + count * 3)..]));
                Vector.Narrow(low, high).CopyTo(destination[index..]);
            }
        }

        for (; index < pixels.Length; index++)
        {
            destination[index] = (byte) pixels[index];
        }
    }

    // destination = left - right per byte, wrapping. The spans are all one row long.
    static void Subtract(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right, Span<byte> destination)
    {
        var index = 0;
        if (Vector.IsHardwareAccelerated)
        {
            for (; index <= left.Length - Vector<byte>.Count; index += Vector<byte>.Count)
            {
                var difference = new Vector<byte>(left[index..]) - new Vector<byte>(right[index..]);
                difference.CopyTo(destination[index..]);
            }
        }

        for (; index < left.Length; index++)
        {
            destination[index] = (byte) (left[index] - right[index]);
        }
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
