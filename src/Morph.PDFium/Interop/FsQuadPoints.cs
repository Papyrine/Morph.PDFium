/// <summary>Mirrors the native FS_QUADPOINTSF struct (four corner points).</summary>
[StructLayout(LayoutKind.Sequential)]
struct FsQuadPoints
{
    public float X1;
    public float Y1;
    public float X2;
    public float Y2;
    public float X3;
    public float Y3;
    public float X4;
    public float Y4;
}
