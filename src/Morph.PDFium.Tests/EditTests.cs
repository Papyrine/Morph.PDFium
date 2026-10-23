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
    [Arguments(double.NaN, 100)]
    [Arguments(100, double.NaN)]
    [Arguments(double.PositiveInfinity, 100)]
    [Arguments(0, 100)]
    [Arguments(100, -5)]
    public async Task NewPageRejectsUnusableSizes(double width, double height)
    {
        using var document = PdfiumDocument.CreateNew();
        await Assert.That(() => document.NewPage(0, width, height)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(document.PageCount).IsEqualTo(0);
    }

    [Test]
    public async Task OpenPageIndexFollowsInsertsAndDeletes()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.multi_page_pdf);
        using var source = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        using var first = document.LoadPage(0);
        using var third = document.LoadPage(2);

        using var inserted = document.NewPage(1, 200, 200);
        await Assert.That(first.Index).IsEqualTo(0);
        await Assert.That(inserted.Index).IsEqualTo(1);
        await Assert.That(third.Index).IsEqualTo(3);

        document.ImportPages(source, index: 0);
        await Assert.That(first.Index).IsEqualTo(1);
        await Assert.That(inserted.Index).IsEqualTo(2);
        await Assert.That(third.Index).IsEqualTo(4);

        document.DeletePage(1);
        await Assert.That(first.Index).IsEqualTo(-1);
        await Assert.That(inserted.Index).IsEqualTo(1);
        await Assert.That(third.Index).IsEqualTo(3);

        // The index still identifies the same page.
        using var reloaded = document.LoadPage(third.Index);
        await Assert.That(reloaded.GetText()).IsEqualTo(third.GetText());
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
    public async Task ImportPagesWithNoIndicesImportsNothing()
    {
        using var source = PdfiumDocument.Load(ProjectFiles.multi_page_pdf);
        using var target = PdfiumDocument.CreateNew();

        // However the empty list is spelled: these reach PDFium as different pointers.
        target.ImportPages(source, ReadOnlySpan<int>.Empty);
        target.ImportPages(source, []);
        target.ImportPages(source, new int[1].AsSpan(0, 0));
        await Assert.That(target.PageCount).IsEqualTo(0);

        target.ImportPages(source, [2]);
        await Assert.That(target.PageCount).IsEqualTo(1);
    }

    [Test]
    [Arguments("")]
    [Arguments("  ")]
    public async Task ImportPagesWithEmptyRangeThrows(string range)
    {
        using var source = PdfiumDocument.Load(ProjectFiles.multi_page_pdf);
        using var target = PdfiumDocument.CreateNew();
        await Assert.That(() => target.ImportPages(source, range)).Throws<ArgumentException>();
        await Assert.That(target.PageCount).IsEqualTo(0);
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

    // PDFium inserts each page before reading the next, so importing a document's own pages
    // ahead of themselves moved them under it: it aborted the process, or copied the wrong page.
    [Test]
    public async Task ImportPagesFromItselfAtIndex()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.multi_page_pdf);
        string?[] original = [.. Enumerable.Range(0, 4).Select(_ => PageText(document, _))];

        document.ImportPages(document, "4", index: 0);
        await Assert.That(document.PageCount).IsEqualTo(5);
        await Assert.That(PageText(document, 0)).IsEqualTo(original[3]);

        document.ImportPages(document, [1, 2], 1);
        await Assert.That(document.PageCount).IsEqualTo(7);
        // The pages that were at 1 and 2 when the import began: original pages 0 and 1.
        await Assert.That(PageText(document, 1)).IsEqualTo(original[0]);
        await Assert.That(PageText(document, 2)).IsEqualTo(original[1]);

        document.ImportPages(document, index: 0);
        await Assert.That(document.PageCount).IsEqualTo(14);
        await Assert.That(PageText(document, 0)).IsEqualTo(original[3]);
        await Assert.That(PageText(document, 7)).IsEqualTo(original[3]);
    }

    // PDFium inserts a blank page for an index past the source's end and then fails, so the
    // indices are checked first: nothing is imported, and the document is as it was.
    [Test]
    public async Task ImportPagesRejectsIndicesPastTheSource()
    {
        using var source = PdfiumDocument.Load(ProjectFiles.multi_page_pdf);
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        using var page = document.LoadPage(0);
        await Assert.That(() => document.ImportPages(source, [99], 0)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => document.ImportPages(source, [0, 1, 4], 0)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => document.ImportPages(source, [-1])).Throws<ArgumentOutOfRangeException>();
        await Assert.That(document.PageCount).IsEqualTo(1);
        await Assert.That(page.Index).IsEqualTo(0);
        using var saved = PdfiumDocument.Load(document.Save());
        await Assert.That(saved.PageCount).IsEqualTo(1);
    }

    [Test]
    public async Task ImportPagesRejectsAnIndexOutsideTheDocument()
    {
        using var source = PdfiumDocument.Load(ProjectFiles.multi_page_pdf);
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        await Assert.That(() => document.ImportPages(source, index: 2)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => document.ImportPages(source, index: -1)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => document.ImportPages(source, [0], 2)).Throws<ArgumentOutOfRangeException>();
        // One past the end appends.
        document.ImportPages(source, "1", index: 1);
        await Assert.That(document.PageCount).IsEqualTo(2);
    }

    // The documented way to import every page at an index: a null range.
    [Test]
    public async Task ImportPagesWithNullRange()
    {
        using var source = PdfiumDocument.Load(ProjectFiles.multi_page_pdf);
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        document.ImportPages(source, null, 0);
        await Assert.That(document.PageCount).IsEqualTo(5);
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
        await Bytes.AreEqual(attachments[0].GetData(), payload);
    }

    [Test]
    public async Task SaveSurfacesStreamException()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        await using var stream = new ThrowingStream();
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
        await Assert.That(attachment.GetData).Throws<ObjectDisposedException>();
        await Assert.That(() => attachment.SetData([4])).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task AttachmentAfterDeleteThrows()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        var deleted = document.AddAttachment("a.bin", [1, 2, 3]);
        var kept = document.AddAttachment("b.bin", [4, 5]);
        document.DeleteAttachment(0);

        await Assert.That(() => deleted.Name).Throws<InvalidOperationException>();
        await Assert.That(deleted.GetData).Throws<InvalidOperationException>();
        await Assert.That(() => deleted.SetData([9])).Throws<InvalidOperationException>();
        await Assert.That(kept.Name).IsEqualTo("b.bin");
        await Assert.That(document.GetAttachments().Single().Name).IsEqualTo("b.bin");

        // The name is free again, and the new attachment is usable.
        var again = document.AddAttachment("a.bin", [7]);
        await Bytes.AreEqual(again.GetData(), [7]);
    }

    // An attachment whose file specification is a dictionary held directly in the name tree,
    // which PDFium frees when it is deleted, and nine held as objects of their own.
    static byte[] PdfWithAttachments()
    {
        var objects = new SortedDictionary<int, string>
        {
            [1] = "<< /Type /Catalog /Pages 2 0 R /Names << /EmbeddedFiles 10 0 R >> >>",
            [2] = "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            [3] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>",
            [11] = CraftedPdf.Stream("direct", "/Type /EmbeddedFile")
        };
        var names = new StringBuilder("(a.txt) << /Type /Filespec /F (a.txt) /EF << /F 11 0 R >> >>");
        for (var index = 0; index < 9; index++)
        {
            var name = $"{(char) ('b' + index)}.txt";
            objects[20 + index * 2] = $"<< /Type /Filespec /F ({name}) /EF << /F {21 + index * 2} 0 R >> >>";
            objects[21 + index * 2] = CraftedPdf.Stream(name, "/Type /EmbeddedFile");
            names.Append($" ({name}) {20 + index * 2} 0 R");
        }

        objects[10] = $"<< /Names [{names}] >>";
        return CraftedPdf.Build(objects);
    }

    // PDFium reuses the address of a deleted attachment, so deletion is tracked on the
    // attachment rather than the address: a deleted one never answers for a new attachment
    // that lands on its address, and a live one that lands on it is not taken for deleted.
    [Test]
    public async Task DeletedAttachmentsAreTrackedApartFromTheirAddress()
    {
        var pdf = PdfWithAttachments();
        for (var round = 0; round < 100; round++)
        {
            using var document = PdfiumDocument.Load(pdf);
            var deleted = document.GetAttachments()[0];
            document.DeleteAttachment(0);
            foreach (var attachment in document.GetAttachments())
            {
                await Assert.That(attachment.Name).IsNotEqualTo("a.txt");
                await Assert.That(attachment.GetData().Length).IsGreaterThan(0);
            }

            for (var index = 0; index < 5; index++)
            {
                document.AddAttachment($"new{index}.bin", [(byte) index]);
            }

            await Assert.That(() => deleted.Name).Throws<InvalidOperationException>();
            await Assert.That(document.GetAttachments().Count).IsEqualTo(14);
        }
    }

    // One page with a line of text and a stamp annotation whose appearance is a red square,
    // which is something Flatten can turn into page content.
    static byte[] PdfWithStamp() =>
        CraftedPdf.Build(new()
        {
            [1] = "<< /Type /Catalog /Pages 2 0 R >>",
            [2] = "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            [3] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> /Annots [6 0 R] >>",
            [4] = CraftedPdf.Stream("BT /F1 24 Tf 72 700 Td (Original) Tj ET"),
            [5] = CraftedPdf.Helvetica,
            [6] = "<< /Type /Annot /Subtype /Stamp /Rect [400 300 500 400] /F 4 /AP << /N 7 0 R >> >>",
            [7] = CraftedPdf.Stream("1 0 0 rg 0 0 100 100 re f", "/Type /XObject /Subtype /Form /BBox [0 0 100 100]")
        });

    [Test]
    public async Task FlattenRefreshesThePage()
    {
        using var document = PdfiumDocument.Load(PdfWithStamp());
        using var page = document.LoadPage(0);
        await Assert.That(page.ObjectCount).IsEqualTo(1);

        await Assert.That(page.Flatten()).IsTrue();

        // The original text plus the flattened stamp.
        await Assert.That(page.ObjectCount).IsEqualTo(2);
        await Assert.That(page.AnnotationCount).IsEqualTo(0);
        await Assert.That(page.Flatten()).IsFalse();
    }

    [Test]
    public async Task EditingAfterFlattenKeepsTheFlattenedContent()
    {
        byte[] bytes;
        using (var document = PdfiumDocument.Load(PdfWithStamp()))
        {
            using var page = document.LoadPage(0);
            _ = page.GetText();
            page.Flatten();
            page.AddText("Added", 72, 100);
            bytes = document.Save();
        }

        using var reloaded = PdfiumDocument.Load(bytes);
        using var reloadedPage = reloaded.LoadPage(0);
        // Original text, flattened stamp and the added text.
        await Assert.That(reloadedPage.ObjectCount).IsEqualTo(3);
        await Assert.That(reloadedPage.GetText()).Contains("Original");
        await Assert.That(reloadedPage.GetText()).Contains("Added");
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
