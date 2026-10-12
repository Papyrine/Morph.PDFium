// Bindings from fpdfview.h for matrix/clip rendering and the render-flag constants.

static partial class PdfiumNative
{
    public const int RenderLcdText = 0x02;
    public const int RenderNoNativeText = 0x04;
    public const int RenderGrayscale = 0x08;
    public const int RenderForPrinting = 0x800;
    public const int RenderNoSmoothText = 0x1000;
    public const int RenderNoSmoothImage = 0x2000;
    public const int RenderNoSmoothPath = 0x4000;

    [LibraryImport(library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDF_PageToDevice(IntPtr page, int startX, int startY, int sizeX, int sizeY, int rotate, double pageX, double pageY, out int deviceX, out int deviceY);

    [LibraryImport(library)]
    internal static partial void FPDF_RenderPageBitmapWithMatrix(IntPtr bitmap, IntPtr page, in FsMatrix matrix, in FsRectF clipping, int flags);
}
