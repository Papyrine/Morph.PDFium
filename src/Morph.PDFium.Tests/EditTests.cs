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
        using var source = PdfiumDocument.Load("multi-page.pdf");
        target.ImportPages(source, "1-2");
        await Assert.That(target.PageCount).IsEqualTo(2);
    }

    [Test]
    public async Task RotateRoundTrips()
    {
        using var document = PdfiumDocument.Load("sample.pdf");
        using var page = document.LoadPage(0);
        page.Rotation = PageRotation.Clockwise90;
        await Assert.That(page.Rotation).IsEqualTo(PageRotation.Clockwise90);
    }

    [Test]
    public async Task AttachmentRoundTrips()
    {
        byte[] payload = [1, 2, 3, 4, 5];
        byte[] bytes;
        using (var document = PdfiumDocument.Load("sample.pdf"))
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
    public async Task AddAnnotation()
    {
        using var document = PdfiumDocument.Load("sample.pdf");
        using var page = document.LoadPage(0);
        var before = page.AnnotationCount;
        page.AddAnnotation(PdfAnnotationType.Square, new(100, 100, 200, 200), "hello");
        await Assert.That(page.AnnotationCount).IsEqualTo(before + 1);
    }

    [Test]
    public async Task DocumentInfoExtras()
    {
        using var document = PdfiumDocument.Load("sample.pdf");
        await Assert.That(document.GetPdfVersion()).IsNotNull();
        await Assert.That(document.GetPermissions()).IsEqualTo(DocumentPermissions.All);
        await Assert.That(document.SignatureCount).IsEqualTo(0);
    }
}
