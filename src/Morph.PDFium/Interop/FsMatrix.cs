/// <summary>Mirrors the native FS_MATRIX struct ([a b c d e f] transform).</summary>
[StructLayout(LayoutKind.Sequential)]
struct FsMatrix
{
    public float A;
    public float B;
    public float C;
    public float D;
    public float E;
    public float F;
}
