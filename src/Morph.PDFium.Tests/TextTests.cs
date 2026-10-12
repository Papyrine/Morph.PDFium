public class TextTests
{
    [Test]
    public async Task ExtractText()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        using var page = document.LoadPage(0);
        var text = page.GetText();
        await Assert.That(text).IsNotNull();
        await Assert.That(page.CharCount).IsGreaterThan(0);
    }

    [Test]
    public async Task ExtractTextRange()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        using var page = document.LoadPage(0);
        var all = page.GetText()!;

        await Assert.That(page.GetText(0, 5)).IsEqualTo(all[..5]);
        await Assert.That(page.GetText(2, -1)).IsEqualTo(all[2..]);
        await Assert.That(page.GetText(0, all.Length + 100)).IsEqualTo(all);
        await Assert.That(page.GetText(0, 0)).IsNull();
        await Assert.That(page.GetText(page.CharCount, 5)).IsNull();
    }

    [Test]
    public async Task ExtractTextRangeNegativeStartThrows()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        using var page = document.LoadPage(0);
        await Assert.That(() => page.GetText(-1, 5)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task TextQueriesSeeTextAddedAfterTheyWereFirstUsed()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        using var page = document.LoadPage(0);
        // Load the page's text, and the web links derived from it, before editing.
        var charsBefore = page.CharCount;
        await Assert.That(page.Search("ZZQXJ").Count).IsEqualTo(0);
        _ = page.GetWebLinks();

        page.AddText("ZZQXJ https://example.com/added", 72, 72);

        await Assert.That(page.GetText()).Contains("ZZQXJ");
        await Assert.That(page.Search("ZZQXJ").Count).IsEqualTo(1);
        await Assert.That(page.CharCount).IsGreaterThan(charsBefore);
        await Assert.That(page.GetWebLinks().Select(_ => _.Url)).Contains("https://example.com/added");
    }

    // Two pages, with links and bookmarks that reach their target in each of the ways a PDF
    // allows: a direct destination, a GoTo action, a jump into another file, a jump into an
    // embedded file, a URI and a launch.
    static byte[] PdfWithNavigation() =>
        CraftedPdf.Build(new()
        {
            [1] = "<< /Type /Catalog /Pages 2 0 R /Outlines 20 0 R >>",
            [2] = "<< /Type /Pages /Kids [3 0 R 6 0 R] /Count 2 >>",
            [3] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> /Annots [10 0 R 11 0 R 12 0 R 13 0 R 14 0 R 15 0 R] >>",
            [4] = CraftedPdf.Stream("BT /F1 24 Tf 72 700 Td (Page) Tj ET"),
            [5] = CraftedPdf.Helvetica,
            [6] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            [10] = "<< /Type /Annot /Subtype /Link /Rect [72 700 300 720] /Dest [6 0 R /XYZ 110 650 2] >>",
            [11] = "<< /Type /Annot /Subtype /Link /Rect [72 670 300 690] /A << /S /GoTo /D [6 0 R /XYZ 10 20 3] >> >>",
            [12] = "<< /Type /Annot /Subtype /Link /Rect [72 640 300 660] /A << /S /GoToR /F (remote.pdf) /D [1 /XYZ 5 6 7] >> >>",
            [13] = "<< /Type /Annot /Subtype /Link /Rect [72 610 300 630] /A << /S /GoToE /T << /R /C /N (embedded.pdf) >> /D [0 /Fit] >> >>",
            [14] = "<< /Type /Annot /Subtype /Link /Rect [72 580 300 600] /A << /S /URI /URI (https://example.com/a?b=c) >> >>",
            [15] = "<< /Type /Annot /Subtype /Link /Rect [72 550 300 570] /A << /S /Launch /F (tool.exe) >> >>",
            [20] = "<< /Type /Outlines /First 21 0 R /Last 23 0 R /Count 3 >>",
            [21] = "<< /Title (Direct) /Parent 20 0 R /Next 22 0 R /Dest [6 0 R /Fit] /First 24 0 R /Last 24 0 R /Count 1 >>",
            [22] = "<< /Title (Local action) /Parent 20 0 R /Prev 21 0 R /Next 23 0 R /A << /S /GoTo /D [6 0 R /Fit] >> >>",
            [23] = "<< /Title (Remote) /Parent 20 0 R /Prev 22 0 R /A << /S /GoToR /F (remote.pdf) /D [1 /Fit] >> >>",
            [24] = "<< /Title <FEFF005A007700650069002000FC> /Parent 21 0 R /A << /S /URI /URI (https://example.org/) >> >>"
        });

    [Test]
    public async Task LinksReportWhereTheyLead()
    {
        using var document = PdfiumDocument.Load(PdfWithNavigation());
        using var page = document.LoadPage(0);
        var links = page.GetLinks();
        await Assert.That(links.Count).IsEqualTo(6);

        // Within this document: a destination and no action.
        await Assert.That(links[0].Destination).IsEqualTo(new PdfDestination(1, 110, 650, 2));
        await Assert.That(links[0].Action).IsNull();
        await Assert.That(links[1].Destination).IsEqualTo(new PdfDestination(1, 10, 20, 3));
        await Assert.That(links[1].Action).IsNull();

        // Into another file: an action carrying the file and the destination within it, and
        // nothing that could be mistaken for a page of this document.
        await Assert.That(links[2].Destination).IsNull();
        await Assert.That(links[2].Action!.Value.Type).IsEqualTo(PdfActionType.RemoteGoTo);
        await Assert.That(links[2].Action!.Value.FilePath).IsEqualTo("remote.pdf");
        await Assert.That(links[2].Action!.Value.Destination).IsEqualTo(new PdfDestination(1, 5, 6, 7));
        await Assert.That(links[3].Destination).IsNull();
        await Assert.That(links[3].Action!.Value.Type).IsEqualTo(PdfActionType.EmbeddedGoTo);

        await Assert.That(links[4].Destination).IsNull();
        await Assert.That(links[4].Action!.Value.Uri).IsEqualTo("https://example.com/a?b=c");
        await Assert.That(links[5].Action!.Value.Type).IsEqualTo(PdfActionType.Launch);
        await Assert.That(links[5].Action!.Value.FilePath).IsEqualTo("tool.exe");
        await Assert.That(links[0].Rectangle).IsEqualTo(new PdfRectangle(72, 700, 300, 720));
    }

    [Test]
    public async Task BookmarksReportWhereTheyLead()
    {
        using var document = PdfiumDocument.Load(PdfWithNavigation());
        var bookmarks = document.GetBookmarks();
        await Assert.That(bookmarks.Select(_ => _.Title)).IsEquivalentTo(["Direct", "Local action", "Remote"]);

        await Assert.That(bookmarks[0].Destination!.Value.PageIndex).IsEqualTo(1);
        await Assert.That(bookmarks[0].Action).IsNull();
        await Assert.That(bookmarks[1].Destination!.Value.PageIndex).IsEqualTo(1);
        await Assert.That(bookmarks[1].Action).IsNull();

        await Assert.That(bookmarks[2].Destination).IsNull();
        await Assert.That(bookmarks[2].Action!.Value.Type).IsEqualTo(PdfActionType.RemoteGoTo);
        await Assert.That(bookmarks[2].Action!.Value.FilePath).IsEqualTo("remote.pdf");

        var child = bookmarks[0].Children.Single();
        await Assert.That(child.Title).IsEqualTo("Zwei \u00fc");
        await Assert.That(child.Action!.Value.Uri).IsEqualTo("https://example.org/");
    }

    [Test]
    public async Task CharGeometry()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        using var page = document.LoadPage(0);
        var chars = page.GetChars();
        await Assert.That(chars.Count).IsGreaterThan(0);
        await Assert.That(chars[0].FontSize).IsGreaterThan(0);
        await Assert.That(chars[0].CodePoint).IsEqualTo(chars[0].Char);
    }
}
