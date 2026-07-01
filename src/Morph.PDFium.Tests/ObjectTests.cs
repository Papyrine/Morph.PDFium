public class ObjectTests
{
    [Test]
    public async Task ReadObjects()
    {
        using var document = PdfiumDocument.Load("sample.pdf");
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
        using (var document = PdfiumDocument.Load("sample.pdf"))
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
    public async Task RemoveObject()
    {
        byte[] bytes;
        int before;
        using (var document = PdfiumDocument.Load("sample.pdf"))
        {
            using var page = document.LoadPage(0);
            before = page.ObjectCount;
            page.RemoveObject(0);
            await Assert.That(page.ObjectCount).IsEqualTo(before - 1);
            bytes = document.Save();
        }

        using var reloaded = PdfiumDocument.Load(bytes);
        using var reloadedPage = reloaded.LoadPage(0);
        await Assert.That(reloadedPage.ObjectCount).IsEqualTo(before - 1);
    }

    [Test]
    public async Task AddLineAndPath()
    {
        byte[] bytes;
        int before;
        using (var document = PdfiumDocument.Load("sample.pdf"))
        {
            using var page = document.LoadPage(0);
            before = page.ObjectCount;
            page.AddLine(new(72, 72), new(272, 172), new(0, 0, 255, 255), width: 2);
            page.AddPath([new(100, 400), new(200, 500), new(300, 400)], fill: new(0, 200, 0, 255), stroke: new(0, 0, 0, 255));
            await Assert.That(page.ObjectCount).IsEqualTo(before + 2);
            bytes = document.Save();
        }

        using var reloaded = PdfiumDocument.Load(bytes);
        using var reloadedPage = reloaded.LoadPage(0);
        await Assert.That(reloadedPage.ObjectCount).IsEqualTo(before + 2);
    }

    [Test]
    public async Task AddImage()
    {
        // An 8x8 opaque-red RGBA buffer.
        var pixels = new byte[8 * 8 * 4];
        for (var pixel = 0; pixel < 8 * 8; pixel++)
        {
            pixels[pixel * 4] = 255;
            pixels[pixel * 4 + 3] = 255;
        }

        byte[] bytes;
        using (var document = PdfiumDocument.Load("sample.pdf"))
        {
            using var page = document.LoadPage(0);
            page.AddImage(pixels, 8, 8, new(72, 72, 172, 172));
            await Assert.That(CountOfType(page, PdfPageObjectType.Image)).IsEqualTo(1);
            bytes = document.Save();
        }

        using var reloaded = PdfiumDocument.Load(bytes);
        using var reloadedPage = reloaded.LoadPage(0);
        await Assert.That(CountOfType(reloadedPage, PdfPageObjectType.Image)).IsEqualTo(1);
    }

    [Test]
    public async Task EditExistingObject()
    {
        using var document = PdfiumDocument.Load("sample.pdf");
        using var page = document.LoadPage(0);
        page.AddRectangle(new(50, 50, 150, 100), new(255, 0, 0, 255));
        // The freshly inserted rectangle is the last object on the page.
        var index = page.ObjectCount - 1;
        page.SetObjectFillColor(index, new(0, 0, 255, 255));
        page.SetObjectStrokeColor(index, new(0, 0, 0, 255));
        page.SetObjectStrokeWidth(index, 3);
        page.MoveObject(index, 20, 30);

        var count = page.ObjectCount;
        var bytes = document.Save();
        using var reloaded = PdfiumDocument.Load(bytes);
        using var reloadedPage = reloaded.LoadPage(0);
        await Assert.That(reloadedPage.ObjectCount).IsEqualTo(count);
    }

    [Test]
    public async Task EditInvalidIndexThrows()
    {
        using var document = PdfiumDocument.Load("sample.pdf");
        using var page = document.LoadPage(0);
        await Assert.That(() => page.RemoveObject(page.ObjectCount)).Throws<ArgumentOutOfRangeException>();
    }

    static int CountOfType(PdfPage page, PdfPageObjectType type)
    {
        var count = 0;
        foreach (var pageObject in page.GetObjects())
        {
            if (pageObject.Type == type)
            {
                count++;
            }
        }

        return count;
    }

    [Test]
    public async Task StructureTreeAndThumbnailDoNotThrow()
    {
        using var document = PdfiumDocument.Load("sample.pdf");
        using var page = document.LoadPage(0);
        // sample.pdf is neither tagged nor has a thumbnail; both should be empty/null, not throw.
        await Assert.That(page.GetStructureTree()).IsNotNull();
        await Assert.That(page.GetEmbeddedThumbnail()).IsNull();
    }
}
