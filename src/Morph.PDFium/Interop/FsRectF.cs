/// <summary>Mirrors the native FS_RECTF struct (a float rectangle in page or device space).</summary>
[StructLayout(LayoutKind.Sequential)]
struct FsRectF
{
    public float Left;
    public float Top;
    public float Right;
    public float Bottom;
}
