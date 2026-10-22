/// <summary>
/// A page rasterised into a buffer rented from the shared array pool, whose first
/// <see cref="Pixels"/>.Length bytes hold the RGBA pixels. Disposing returns the buffer, so
/// nothing that refers to it may be kept past that.
/// </summary>
readonly struct RenderedPixels(byte[] buffer, int width, int height) :
    IDisposable
{
    public int Width { get; } = width;
    public int Height { get; } = height;

    public ReadOnlySpan<byte> Pixels =>
        buffer.AsSpan(0, Width * Height * 4);

    public void Dispose() =>
        ArrayPool<byte>.Shared.Return(buffer);
}
