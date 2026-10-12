public class EditTests
{
    [Test]
    public async Task CreateSaveReload()
    {
        byte[] bytes;
        using (var document = PdfiumDocument.CreateNew())
        {
            using (document.NewPage(0, 200, 300)) { }
            using (document.NewPage(1, 200, 300)) { }
            bytes = document.Save();
        }

        using var reloaded = PdfiumDocument.Load(bytes);
        await Assert.That(reloaded.PageCount).IsEqualTo(2);
    }

    [Test]
    public async Task ImportPages()
    {
        using var target = PdfiumDocument.CreateNew();
        using var source = PdfiumDocument.Load(ProjectFiles.multi_page_pdf);
        target.ImportPages(source, "1-2");
        await Assert.That(target.PageCount).IsEqualTo(2);
    }

    [Test]
    public async Task RotateRoundTrips()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        using var page = document.LoadPage(0);
        page.Rotation = PageRotation.Clockwise90;
        await Assert.That(page.Rotation).IsEqualTo(PageRotation.Clockwise90);
    }

    [Test]
    public async Task ImportPagesAppendsByDefault()
    {
        using var first = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        using var second = PdfiumDocument.Load(ProjectFiles.multi_page_pdf);
        using var merged = PdfiumDocument.CreateNew();
        merged.ImportPages(first);
        merged.ImportPages(second, "1-2");
        merged.ImportPages(second, [3]);

        await Assert.That(merged.PageCount).IsEqualTo(4);
        await Assert.That(PageText(merged, 0)).IsEqualTo(PageText(first, 0));
        await Assert.That(PageText(merged, 1)).IsEqualTo(PageText(second, 0));
        await Assert.That(PageText(merged, 2)).IsEqualTo(PageText(second, 1));
        await Assert.That(PageText(merged, 3)).IsEqualTo(PageText(second, 3));
    }

    [Test]
    public async Task ImportPagesAtIndexInsertsBeforeIt()
    {
        using var first = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        using var second = PdfiumDocument.Load(ProjectFiles.multi_page_pdf);
        using var merged = PdfiumDocument.CreateNew();
        merged.ImportPages(first);
        merged.ImportPages(second, "1", index: 0);

        await Assert.That(PageText(merged, 0)).IsEqualTo(PageText(second, 0));
        await Assert.That(PageText(merged, 1)).IsEqualTo(PageText(first, 0));
    }

    static string? PageText(PdfiumDocument document, int index)
    {
        using var page = document.LoadPage(index);
        return page.GetText();
    }

    [Test]
    public async Task AttachmentRoundTrips()
    {
        byte[] payload = [1, 2, 3, 4, 5];
        byte[] bytes;
        using (var document = PdfiumDocument.Load(ProjectFiles.sample_pdf))
        {
            document.AddAttachment("data.bin", payload);
            bytes = document.Save();
        }

        using var reloaded = PdfiumDocument.Load(bytes);
        var attachments = reloaded.GetAttachments();
        await Assert.That(attachments.Count).IsEqualTo(1);
        await Assert.That(attachments[0].Name).IsEqualTo("data.bin");
        await Assert.That(attachments[0].GetData()).IsEquivalentTo(payload);
    }

    [Test]
    public async Task SaveSurfacesStreamException()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        using var stream = new ThrowingStream();
        var exception = await Assert.That(() => document.Save(stream)).Throws<IOException>();
        await Assert.That(exception!.Message).IsEqualTo("disk full");
    }

    class ThrowingStream :
        MemoryStream
    {
        public override void Write(ReadOnlySpan<byte> buffer) =>
            throw new IOException("disk full");
    }

    [Test]
    public async Task AttachmentAfterDocumentDisposedThrows()
    {
        var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        var attachment = document.AddAttachment("data.bin", [1, 2, 3]);
        document.Dispose();
        await Assert.That(() => attachment.Name).Throws<ObjectDisposedException>();
        await Assert.That(() => attachment.GetData()).Throws<ObjectDisposedException>();
        await Assert.That(() => attachment.SetData([4])).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task AddAnnotation()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        using var page = document.LoadPage(0);
        var before = page.AnnotationCount;
        page.AddAnnotation(PdfAnnotationType.Square, new(100, 100, 200, 200), "hello");
        await Assert.That(page.AnnotationCount).IsEqualTo(before + 1);
    }

    [Test]
    public async Task DocumentInfoExtras()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        await Assert.That(document.GetPdfVersion()).IsNotNull();
        await Assert.That(document.GetPermissions()).IsEqualTo(DocumentPermissions.All);
        await Assert.That(document.SignatureCount).IsEqualTo(0);
    }
}
