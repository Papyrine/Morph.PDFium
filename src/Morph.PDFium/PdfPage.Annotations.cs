namespace Morph.PDFium;

public sealed partial class PdfPage
{
    static ReadOnlySpan<byte> ContentsKey => "Contents\0"u8;

    /// <summary>The number of annotations on the page.</summary>
    public int AnnotationCount
    {
        get
        {
            var page = ValidHandle();
            lock (PdfiumNative.Sync)
            {
                return Math.Max(0, PdfiumNative.FPDFPage_GetAnnotCount(page));
            }
        }
    }

    /// <summary>
    /// Reads all annotations on the page into managed records. Note that widget annotations
    /// belong to interactive form fields; their values are read through the forms API.
    /// </summary>
    public IReadOnlyList<PdfAnnotation> GetAnnotations()
    {
        var page = ValidHandle();
        lock (PdfiumNative.Sync)
        {
            var count = PdfiumNative.FPDFPage_GetAnnotCount(page);
            if (count <= 0)
            {
                return [];
            }

            var annotations = new List<PdfAnnotation>(count);
            for (var index = 0; index < count; index++)
            {
                var annot = PdfiumNative.FPDFPage_GetAnnot(page, index);
                if (annot == IntPtr.Zero)
                {
                    continue;
                }

                try
                {
                    annotations.Add(Read(index, annot));
                }
                finally
                {
                    PdfiumNative.FPDFPage_CloseAnnot(annot);
                }
            }

            return annotations;
        }
    }

    /// <summary>
    /// Creates an annotation of the given <paramref name="type"/> with the supplied rectangle
    /// and optional text contents, and returns it. Persist with <see cref="PdfiumDocument.Save(SaveFlags,int?)"/>.
    /// </summary>
    public PdfAnnotation AddAnnotation(PdfAnnotationType type, PdfRectangle rectangle, string? contents = null)
    {
        var page = ValidHandle();
        lock (PdfiumNative.Sync)
        {
            var annot = PdfiumNative.FPDFPage_CreateAnnot(page, (int) type);
            if (annot == IntPtr.Zero)
            {
                throw new PdfiumException($"Failed to create {type} annotation (unsupported subtype?)");
            }

            try
            {
                var rect = new FsRectF { Left = (float) rectangle.Left, Top = (float) rectangle.Top, Right = (float) rectangle.Right, Bottom = (float) rectangle.Bottom };
                PdfiumNative.FPDFAnnot_SetRect(annot, in rect);
                if (contents is not null)
                {
                    PdfiumNative.FPDFAnnot_SetStringValue(annot, ContentsKey, Interop.ToWideString(contents));
                }

                var index = PdfiumNative.FPDFPage_GetAnnotCount(page) - 1;
                return Read(index, annot);
            }
            finally
            {
                PdfiumNative.FPDFPage_CloseAnnot(annot);
            }
        }
    }

    /// <summary>Removes the annotation at <paramref name="index"/>.</summary>
    public void RemoveAnnotation(int index)
    {
        var page = ValidHandle();
        lock (PdfiumNative.Sync)
        {
            if (!PdfiumNative.FPDFPage_RemoveAnnot(page, index))
            {
                throw new PdfiumException($"Failed to remove annotation at index {index}");
            }
        }
    }

    // Caller must hold PdfiumNative.Sync and an open annot handle.
    static PdfAnnotation Read(int index, IntPtr annot)
    {
        var type = (PdfAnnotationType) PdfiumNative.FPDFAnnot_GetSubtype(annot);
        var rectangle = PdfiumNative.FPDFAnnot_GetRect(annot, out var rect)
            ? new PdfRectangle(rect.Left, rect.Bottom, rect.Right, rect.Top)
            : default;
        var contents = Interop.Utf16ByLength((buffer, length) => PdfiumNative.FPDFAnnot_GetStringValue(annot, ContentsKey, buffer, length));
        PdfColor? color = PdfiumNative.FPDFAnnot_GetColor(annot, 0, out var r, out var g, out var b, out var a)
            ? new PdfColor((byte) r, (byte) g, (byte) b, (byte) a)
            : null;
        return new(index, type, rectangle, contents, color);
    }
}
