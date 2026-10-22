/// <summary>
/// Minimal PNG writer (8 bit grayscale or RGBA, None or Up filter, zlib) so the package
/// carries no image library dependency. A pHYs chunk records the render DPI so consumers
/// display the page at its physical size.
/// </summary>
static class PngEncoder
{
    static readonly byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    static readonly uint[] crcTables = BuildCrcTables();

    // PNG scanline filter types.
    const byte None = 0;
    const byte Up = 2;
    // How many rows the filter trial compresses.
    const int sampleRows = 64;

    // compressionLevel is a zlib level, 0 to 9, validated by RenderOptions.
    public static byte[] Encode(ReadOnlySpan<byte> rgba, int width, int height, double dpi, int compressionLevel)
    {
        // Most pages are dark text on a white background, which anti-aliases to shades of
        // gray. Such a page loses nothing as a one byte per pixel grayscale image, and it
        // gives deflate a quarter of the data, so it encodes faster and to a smaller file.
        // One pass over the pixels both settles whether this is such a page and, when it is,
        // extracts the gray plane that the rest of the encoder then reads in place of rgba.
        var pixelCount = width * height;
        var plane = ArrayPool<byte>.Shared.Rent(pixelCount);
        try
        {
            var gray = TryExtractGrayPlane(rgba, plane.AsSpan(0, pixelCount));
            var stride = ScanlineLength(width, gray);
            var scanlines = rgba;
            if (gray)
            {
                scanlines = plane.AsSpan(0, pixelCount);
            }

            var filter = ChooseFilter(scanlines, stride, height);
            // A page's PNG runs to hundreds of kilobytes, so the output grows through pooled
            // buffers rather than a fresh array at each doubling.
            using var output = new PooledBuffer(rgba.Length / 8);
            output.Write(signature);
            WriteChunk(output, "IHDR"u8, BuildHeader(width, height, gray));
            WriteChunk(output, "pHYs"u8, BuildPhysicalDimensions(dpi));
            WriteImageData(output, scanlines, stride, height, filter, compressionLevel);
            WriteChunk(output, "IEND"u8, []);
            return output.ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(plane);
        }
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

    // Whether every pixel is opaque with equal red, green and blue, writing each pixel's red
    // byte, which is then its level, to plane along the way. Read as a little endian word an
    // RGBA pixel is R | G << 8 | B << 16 | A << 24, so it qualifies when it equals its red byte
    // copied into the other two color bytes with the alpha byte set. Stops at the first pixel
    // that does not qualify, leaving the rest of plane unwritten.
    static bool TryExtractGrayPlane(ReadOnlySpan<byte> rgba, Span<byte> plane)
    {
        if (!BitConverter.IsLittleEndian)
        {
            return false;
        }

        var pixels = MemoryMarshal.Cast<byte, uint>(rgba);
        pixels = pixels[..Math.Min(pixels.Length, plane.Length)];
        var index = 0;
        if (Vector256.IsHardwareAccelerated)
        {
            if (!ExtractGray256(pixels, plane, ref index))
            {
                return false;
            }
        }
        else if (Vector128.IsHardwareAccelerated)
        {
            if (!ExtractGray128(pixels, plane, ref index))
            {
                return false;
            }
        }

        for (; index < pixels.Length; index++)
        {
            var pixel = pixels[index];
            if (pixel != (((pixel & 0xFF) * 0x010101) | 0xFF000000))
            {
                return false;
            }

            plane[index] = (byte) pixel;
        }

        return true;
    }

    // The vector form of the loop above, in blocks of four vectors, advancing index past the
    // blocks done; false once a block holds a pixel that is not opaque gray. Once masked to
    // the red byte every value fits the narrower type, so the saturating narrows, which have
    // single instruction forms, cannot clip and act as truncation.
    static bool ExtractGray256(ReadOnlySpan<uint> pixels, Span<byte> plane, ref int index)
    {
        var red = Vector256.Create(0xFFu);
        var spread = Vector256.Create(0x010101u);
        var opaque = Vector256.Create(0xFF000000u);
        var lanes = Vector256<uint>.Count;
        ref var source = ref MemoryMarshal.GetReference(pixels);
        ref var target = ref MemoryMarshal.GetReference(plane);
        for (; index <= pixels.Length - lanes * 4; index += lanes * 4)
        {
            var first = Vector256.LoadUnsafe(ref source, (nuint) index);
            var second = Vector256.LoadUnsafe(ref source, (nuint) (index + lanes));
            var third = Vector256.LoadUnsafe(ref source, (nuint) (index + lanes * 2));
            var fourth = Vector256.LoadUnsafe(ref source, (nuint) (index + lanes * 3));
            var firstRed = first & red;
            var secondRed = second & red;
            var thirdRed = third & red;
            var fourthRed = fourth & red;
            var gray = Vector256.Equals(first, (firstRed * spread) | opaque)
                       & Vector256.Equals(second, (secondRed * spread) | opaque)
                       & Vector256.Equals(third, (thirdRed * spread) | opaque)
                       & Vector256.Equals(fourth, (fourthRed * spread) | opaque);
            if (gray != Vector256<uint>.AllBitsSet)
            {
                return false;
            }

            var low = Vector256.NarrowWithSaturation(firstRed, secondRed);
            var high = Vector256.NarrowWithSaturation(thirdRed, fourthRed);
            Vector256.NarrowWithSaturation(low, high).StoreUnsafe(ref target, (nuint) index);
        }

        return true;
    }

    static bool ExtractGray128(ReadOnlySpan<uint> pixels, Span<byte> plane, ref int index)
    {
        var red = Vector128.Create(0xFFu);
        var spread = Vector128.Create(0x010101u);
        var opaque = Vector128.Create(0xFF000000u);
        var lanes = Vector128<uint>.Count;
        ref var source = ref MemoryMarshal.GetReference(pixels);
        ref var target = ref MemoryMarshal.GetReference(plane);
        for (; index <= pixels.Length - lanes * 4; index += lanes * 4)
        {
            var first = Vector128.LoadUnsafe(ref source, (nuint) index);
            var second = Vector128.LoadUnsafe(ref source, (nuint) (index + lanes));
            var third = Vector128.LoadUnsafe(ref source, (nuint) (index + lanes * 2));
            var fourth = Vector128.LoadUnsafe(ref source, (nuint) (index + lanes * 3));
            var firstRed = first & red;
            var secondRed = second & red;
            var thirdRed = third & red;
            var fourthRed = fourth & red;
            var gray = Vector128.Equals(first, (firstRed * spread) | opaque)
                       & Vector128.Equals(second, (secondRed * spread) | opaque)
                       & Vector128.Equals(third, (thirdRed * spread) | opaque)
                       & Vector128.Equals(fourth, (fourthRed * spread) | opaque);
            if (gray != Vector128<uint>.AllBitsSet)
            {
                return false;
            }

            var low = Vector128.NarrowWithSaturation(firstRed, secondRed);
            var high = Vector128.NarrowWithSaturation(thirdRed, fourthRed);
            Vector128.NarrowWithSaturation(low, high).StoreUnsafe(ref target, (nuint) index);
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
    static void WriteImageData(PooledBuffer output, ReadOnlySpan<byte> scanlines, int stride, int height, byte filter, int compressionLevel)
    {
        var start = (int) output.Length;
        Span<byte> word = stackalloc byte[4];
        output.Write(word); // length, patched below
        output.Write("IDAT"u8);
        Compress(output, scanlines, stride, height, filter, compressionLevel);

        var chunk = output.Written[start..];
        BinaryPrimitives.WriteInt32BigEndian(chunk, chunk.Length - 8);
        // The CRC covers the chunk type and data but not the length.
        var crc = UpdateCrc(uint.MaxValue, chunk[4..]);
        BinaryPrimitives.WriteUInt32BigEndian(word, crc ^ uint.MaxValue);
        output.Write(word);
    }

    // scanlines holds the image's rows, stride bytes each, in the form PNG stores them: the gray
    // plane, or the RGBA pixels as they are.
    static void Compress(Stream output, ReadOnlySpan<byte> scanlines, int stride, int height, byte filter, int compressionLevel)
    {
        var options = new ZLibCompressionOptions
        {
            CompressionLevel = compressionLevel
        };
        using var zlib = new ZLibStream(output, options, leaveOpen: true);
        // Each scanline goes to zlib in one write: its filter type byte, then the filtered row.
        var filtered = new byte[stride + 1];
        filtered[0] = filter;
        // The Up filter subtracts the row above; above the first row that is defined as all zeros.
        ReadOnlySpan<byte> previous = new byte[stride];
        for (var y = 0; y < height; y++)
        {
            var row = scanlines.Slice(y * stride, stride);
            Filter(filter, row, previous, filtered.AsSpan(1));
            zlib.Write(filtered);
            previous = row;
        }
    }

    // Deflate does well on rows that repeat earlier data. On a page of text the unfiltered rows
    // do: the same glyph shapes recur all the way down it, and the Up filter, which replaces
    // each row with its difference from the one above, hides that. On a photograph or a
    // gradient the opposite holds: neighbouring rows are alike but little else repeats, so Up
    // turns most of the image into runs of near zero. Which kind of image this is gets settled
    // by compressing a sample of its rows both ways at the fastest level and keeping whichever
    // came out smaller.
    static byte ChooseFilter(ReadOnlySpan<byte> scanlines, int stride, int height)
    {
        var step = Math.Max(1, height / sampleRows);
        using var noneSize = new CountingStream();
        using var upSize = new CountingStream();
        using (var none = new ZLibStream(noneSize, CompressionLevel.Fastest, leaveOpen: true))
        using (var up = new ZLibStream(upSize, CompressionLevel.Fastest, leaveOpen: true))
        {
            var filtered = new byte[stride + 1];
            ReadOnlySpan<byte> zeros = new byte[stride];
            for (var y = 0; y < height; y += step)
            {
                var row = scanlines.Slice(y * stride, stride);
                var previous = y == 0 ? zeros : scanlines.Slice((y - 1) * stride, stride);
                filtered[0] = None;
                Filter(None, row, previous, filtered.AsSpan(1));
                none.Write(filtered);
                filtered[0] = Up;
                Filter(Up, row, previous, filtered.AsSpan(1));
                up.Write(filtered);
            }
        }

        if (upSize.Length < noneSize.Length)
        {
            return Up;
        }

        return None;
    }

    static int ScanlineLength(int width, bool gray)
    {
        if (gray)
        {
            return width;
        }

        return width * 4;
    }

    static void Filter(byte filter, ReadOnlySpan<byte> row, ReadOnlySpan<byte> previous, Span<byte> destination)
    {
        if (filter == Up)
        {
            Subtract(row, previous, destination);
            return;
        }

        row.CopyTo(destination);
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

    // CRC-32 over the PNG polynomial by slicing-by-eight: eight bytes per step through eight
    // tables rather than one byte per step through one. The byte at a time loop depends on
    // its own previous result, so it ran at a few cycles per byte and took a noticeable share
    // of encoding time; this is three to four times faster without any platform specific code.
    static uint UpdateCrc(uint crc, ReadOnlySpan<byte> data)
    {
        var index = 0;
        for (; index + 8 <= data.Length; index += 8)
        {
            var one = BinaryPrimitives.ReadUInt32LittleEndian(data[index..]) ^ crc;
            var two = BinaryPrimitives.ReadUInt32LittleEndian(data[(index + 4)..]);
            crc = crcTables[7 * 256 + (one & 0xFF)]
                  ^ crcTables[6 * 256 + ((one >> 8) & 0xFF)]
                  ^ crcTables[5 * 256 + ((one >> 16) & 0xFF)]
                  ^ crcTables[4 * 256 + (one >> 24)]
                  ^ crcTables[3 * 256 + (two & 0xFF)]
                  ^ crcTables[2 * 256 + ((two >> 8) & 0xFF)]
                  ^ crcTables[256 + ((two >> 16) & 0xFF)]
                  ^ crcTables[two >> 24];
        }

        for (; index < data.Length; index++)
        {
            crc = crcTables[(crc ^ data[index]) & 0xFF] ^ (crc >> 8);
        }

        return crc;
    }

    // Eight tables of 256 entries. Table k holds, for each byte value, the CRC of that byte
    // followed by k zero bytes, which is what lets eight bytes be folded in at once.
    static uint[] BuildCrcTables()
    {
        var tables = new uint[8 * 256];
        for (uint index = 0; index < 256; index++)
        {
            var entry = index;
            for (var bit = 0; bit < 8; bit++)
            {
                entry = (entry & 1) == 1 ? 0xEDB88320 ^ (entry >> 1) : entry >> 1;
            }

            tables[index] = entry;
        }

        // Each further table is the previous one advanced by a zero byte, which needs the whole
        // of the first table, hence the separate loop.
        for (var k = 1; k < 8; k++)
        {
            for (var index = 0; index < 256; index++)
            {
                var previous = tables[(k - 1) * 256 + index];
                tables[k * 256 + index] = tables[previous & 0xFF] ^ (previous >> 8);
            }
        }

        return tables;
    }

    // A write-only stream over an array rented from the shared pool, grown by renting a larger
    // one. Disposing returns the array, so ToArray must be called before that.
    sealed class PooledBuffer(int capacity) :
        Stream
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(capacity);
        int length;

        public Span<byte> Written => buffer.AsSpan(0, length);

        public byte[] ToArray() =>
            Written.ToArray();

        public override void Write(ReadOnlySpan<byte> source)
        {
            if (source.Length > buffer.Length - length)
            {
                var needed = Math.Max(2L * buffer.Length, (long) length + source.Length);
                var grown = ArrayPool<byte>.Shared.Rent((int) Math.Min(needed, Array.MaxLength));
                Written.CopyTo(grown);
                ArrayPool<byte>.Shared.Return(buffer);
                buffer = grown;
            }

            source.CopyTo(buffer.AsSpan(length));
            length += source.Length;
        }

        public override void Write(byte[] source, int offset, int count) =>
            Write(source.AsSpan(offset, count));

        public override void WriteByte(byte value) =>
            Write(new ReadOnlySpan<byte>(in value));

        protected override void Dispose(bool disposing)
        {
            if (buffer.Length != 0)
            {
                ArrayPool<byte>.Shared.Return(buffer);
                buffer = [];
            }

            base.Dispose(disposing);
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => length;

        public override long Position
        {
            get => length;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] destination, int offset, int count) =>
            throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();
    }

    // Discards what is written to it and remembers only how much that was.
    sealed class CountingStream :
        Stream
    {
        long length;

        public override void Write(ReadOnlySpan<byte> source) =>
            length += source.Length;

        public override void Write(byte[] source, int offset, int count) =>
            length += count;

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => length;

        public override long Position
        {
            get => length;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] destination, int offset, int count) =>
            throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();
    }
}
