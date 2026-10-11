public class ObjectTests
{
    [Test]
    public async Task ReadObjects()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        using var page = document.LoadPage(0);
        var objects = page.GetObjects();
        await Assert.That(objects.Count).IsEqualTo(page.ObjectCount);
        await Assert.That(objects.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task AddTextAndRectangle()
    {
        byte[] bytes;
        int before;
        int afterAdds;
        using (var document = PdfiumDocument.Load(ProjectFiles.sample_pdf))
        {
            using var page = document.LoadPage(0);
            before = page.ObjectCount;
            page.AddRectangle(new(50, 50, 150, 100), new(255, 0, 0, 255));
            page.AddText("Added by Morph.PDFium", 72, 72, fontSize: 14);
            afterAdds = page.ObjectCount;
            bytes = document.Save();
        }

        using var reloaded = PdfiumDocument.Load(bytes);
        using var reloadedPage = reloaded.LoadPage(0);
        await Assert.That(afterAdds).IsEqualTo(before + 2);
        await Assert.That(reloadedPage.ObjectCount).IsEqualTo(before + 2);
    }

    [Test]
    public async Task EditsPersistWhenPageDisposedBeforeSave()
    {
        byte[] bytes;
        int before;
        using (var document = PdfiumDocument.Load(ProjectFiles.sample_pdf))
        {
            using (var page = document.LoadPage(0))
            {
                before = page.ObjectCount;
                page.AddRectangle(new(50, 50, 150, 100), new(255, 0, 0, 255));
                page.AddText("first", 72, 72);
                page.AddText("second", 72, 90);
            }

            bytes = document.Save();
        }

        using var reloaded = PdfiumDocument.Load(bytes);
        using var reloadedPage = reloaded.LoadPage(0);
        await Assert.That(reloadedPage.ObjectCount).IsEqualTo(before + 3);
    }

    [Test]
    public async Task EditsVisibleToRenderWhilePageOpen()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        var original = document.RenderPage(0);
        using var page = document.LoadPage(0);
        page.AddRectangle(new(50, 50, 300, 300), new(255, 0, 0, 255));
        var edited = document.RenderPage(0);
        await Assert.That(edited.AsSpan().SequenceEqual(original)).IsFalse();
    }

    [Test]
    public async Task EditsVisibleToImportWhilePageOpen()
    {
        using var source = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        using var page = source.LoadPage(0);
        var before = page.ObjectCount;
        page.AddText("imported", 72, 72);

        using var target = PdfiumDocument.CreateNew();
        target.ImportPages(source);
        using var imported = target.LoadPage(0);
        await Assert.That(imported.ObjectCount).IsEqualTo(before + 1);
    }

    [Test]
    public async Task StructureTreeAndThumbnailDoNotThrow()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        using var page = document.LoadPage(0);
        // sample.pdf is neither tagged nor has a thumbnail; both should be empty/null, not throw.
        await Assert.That(page.GetStructureTree()).IsNotNull();
        await Assert.That(page.GetEmbeddedThumbnail()).IsNull();
    }
}
