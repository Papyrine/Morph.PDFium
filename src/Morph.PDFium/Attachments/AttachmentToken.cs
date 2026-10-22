/// <summary>
/// Whether the <see cref="Morph.PDFium.PdfAttachment"/>s wrapping one native attachment
/// handle have been deleted. Shared by them all, so a delete reaches each.
/// </summary>
sealed class AttachmentToken
{
    public bool Deleted { get; set; }
}
