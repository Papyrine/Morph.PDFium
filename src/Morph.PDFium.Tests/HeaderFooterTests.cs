public class HeaderFooterTests
{
    const string body = "0 0 1 rg 72 300 300 200 re f 0 g BT /F1 24 Tf 72 600 Td (Body text) Tj ET";
    const string header = "/Artifact << /Type /Pagination /Subtype /Header >> BDC 1 0 0 rg 0 740 612 52 re f 0 g BT /F1 12 Tf 72 760 Td (Running header) Tj ET EMC";
    const string footer = "/Artifact << /Type /Pagination /Subtype /Footer >> BDC BT /F1 10 Tf 72 30 Td (Page footer https://example.com/footer) Tj ET EMC";

    static readonly HtmlOptions leftOut = new()
    {
        IncludeHeadersAndFooters = false
    };

    [Test]
    public async Task LeavesOutHeaderAndFooterText()
    {
        using var document = PdfiumDocument.Load(HtmlTests.SinglePage($"{header} {body} {footer}"));
        foreach (var text in new[] { HtmlTextMode.Overlay, HtmlTextMode.Visible })
        {
            var html = document.RenderPageHtml(0, leftOut with { IncludeImage = false, Text = text });
            await Assert.That(html).Contains(">Body text</text>");
            await Assert.That(html).DoesNotContain("Running header");
            await Assert.That(html).DoesNotContain("Page footer");
            await Assert.That(html).DoesNotContain("example.com");
        }

        // Kept by default.
        var kept = document.RenderPageHtml(0, new() { IncludeImage = false });
        await Assert.That(kept).Contains("Running header");
        await Assert.That(kept).Contains("Page footer");
        await Assert.That(kept).Contains("<a href=\"https://example.com/footer\"");
    }

    [Test]
    public async Task LeavesOutHeaderAndFooterPixels()
    {
        using var document = PdfiumDocument.Load(HtmlTests.SinglePage($"{header} {body} {footer}"));
        var withoutThem = RenderOf(body);
        await Bytes.AreEqual(Image(document, leftOut), withoutThem);
        await Bytes.AreEqual(Pixels(document, leftOut), Pixels(PdfiumDocument.Load(HtmlTests.SinglePage(body)), new()));
        // Kept by default, and the default image is the plain render.
        await Bytes.AreEqual(Image(document, new()), document.RenderPage(0));
        // With visible text too, both go.
        await Bytes.AreEqual(Image(document, leftOut with { Text = HtmlTextMode.Visible }), RenderOf("0 0 1 rg 72 300 300 200 re f"));
    }

    [Test]
    public async Task LeavesOutHeadersAndFootersInForms()
    {
        // A form drawn within a header goes whole; a footer marked within a form goes too.
        // The header's copy of the form is shrunk into the top margin.
        const string form = "0 1 0 rg 400 650 100 50 re f BT /F1 10 Tf 400 640 Td (Form body) Tj ET";
        var pdf = HtmlTests.SinglePage(
            $"/Artifact << /Type /Pagination /Subtype /Header >> BDC q 0.5 0 0 0.1 0 700 cm /Fm1 Do Q EMC /Fm1 Do {body}",
            $"/Artifact << /Type /Pagination /Subtype /Footer >> BDC BT /F1 10 Tf 72 40 Td (Form footer) Tj ET EMC {form}");
        using var document = PdfiumDocument.Load(pdf);
        var html = document.RenderPageHtml(0, leftOut with { IncludeImage = false });
        await Assert.That(html).DoesNotContain("Form footer");
        await Assert.That(html.Split("Form body").Length - 1).IsEqualTo(1);
        await Bytes.AreEqual(Image(document, leftOut), RenderOf($"/Fm1 Do {body}", form));
    }

    [Test]
    public async Task LeavesOutLinksInHeaders()
    {
        var pdf = CraftedPdf.Build(new()
        {
            [1] = "<< /Type /Catalog /Pages 2 0 R >>",
            [2] = "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            [3] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> /Annots [6 0 R 7 0 R] >>",
            [4] = CraftedPdf.Stream($"{header} {body}"),
            [5] = CraftedPdf.Helvetica,
            [6] = "<< /Type /Annot /Subtype /Link /Rect [72 755 160 775] /A << /S /URI /URI (https://example.com/header) >> >>",
            [7] = "<< /Type /Annot /Subtype /Link /Rect [72 595 160 625] /A << /S /URI /URI (https://example.com/body) >> >>"
        });
        using var document = PdfiumDocument.Load(pdf);
        var html = document.RenderPageHtml(0, leftOut with { IncludeImage = false });
        await Assert.That(html).DoesNotContain("example.com/header");
        await Assert.That(html).Contains("<a href=\"https://example.com/body\"");
        await Assert.That(document.RenderPageHtml(0, new() { IncludeImage = false })).Contains("example.com/header");
    }

    [Test]
    public async Task KeepsOtherArtifactsAndUntaggedContent()
    {
        // Layout and background artifacts, an untagged header-like line, and other marked
        // content stay.
        var content =
            """
            /Artifact << /Type /Layout >> BDC BT /F1 12 Tf 72 700 Td (Layout artifact) Tj ET EMC
            /Artifact << /Type /Background >> BDC BT /F1 12 Tf 72 680 Td (Background artifact) Tj ET EMC
            /Artifact BDC BT /F1 12 Tf 72 660 Td (Bare artifact) Tj ET EMC
            /Artifact BMC BT /F1 12 Tf 72 640 Td (Unmarked properties) Tj ET EMC
            /Span << /Type /Pagination /Subtype /Header >> BDC BT /F1 12 Tf 72 620 Td (Not an artifact) Tj ET EMC
            BT /F1 12 Tf 72 760 Td (Untagged header) Tj ET
            """;
        using var document = PdfiumDocument.Load(HtmlTests.SinglePage(content));
        await Assert.That(document.RenderPageHtml(0, leftOut)).IsEqualTo(document.RenderPageHtml(0));
    }

    [Test]
    public async Task KeepsWatermarks()
    {
        // PDFium cannot read the subtype names, so a pagination artifact counts as a header or
        // footer only when it lies wholly in the top or bottom margin band: a watermark across
        // the body stays, as does one reaching from the header into the body.
        var content =
            $"""
             {header} {body}
             /Artifact << /Type /Pagination /Subtype /Watermark >> BDC 0.8 g BT /F1 60 Tf 100 380 Td (CONFIDENTIAL) Tj ET EMC
             /Artifact << /Type /Pagination /Subtype /Watermark >> BDC 0.5 g 500 300 50 480 re f EMC
             """;
        using var document = PdfiumDocument.Load(HtmlTests.SinglePage(content));
        var html = document.RenderPageHtml(0, leftOut with { IncludeImage = false });
        await Assert.That(html).Contains(">CONFIDENTIAL</text>");
        await Assert.That(html).DoesNotContain("Running header");
        var kept = $"""
                    {body}
                    0.8 g BT /F1 60 Tf 100 380 Td (CONFIDENTIAL) Tj ET
                    0.5 g 500 300 50 480 re f
                    """;
        await Bytes.AreEqual(Image(document, leftOut), RenderOf(kept));
    }

    [Test]
    public async Task StringSubtypesGoWherever()
    {
        // A subtype written as a string can be read, so it needs no band.
        using var document = PdfiumDocument.Load(HtmlTests.SinglePage($"/Artifact << /Type (Pagination) /Subtype (Header) >> BDC BT /F1 12 Tf 72 400 Td (Mid header) Tj ET EMC /Artifact << /Type (Pagination) /Subtype (Watermark) >> BDC BT /F1 12 Tf 72 20 Td (Low watermark) Tj ET EMC {body}"));
        var html = document.RenderPageHtml(0, leftOut with { IncludeImage = false });
        await Assert.That(html).DoesNotContain("Mid header");
        await Assert.That(html).Contains("Low watermark");
    }

    [Test]
    public async Task BandsFollowRotation()
    {
        // Turned a quarter, the page's left edge is the top of the displayed page.
        var pdf = CraftedPdf.Build(new()
        {
            [1] = "<< /Type /Catalog /Pages 2 0 R >>",
            [2] = "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            [3] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Rotate 90 /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            [4] = CraftedPdf.Stream(
                """
                /Artifact << /Type /Pagination /Subtype /Header >> BDC BT /F1 12 Tf 0 1 -1 0 40 100 Tm (Side header) Tj ET EMC
                /Artifact << /Type /Pagination /Subtype /Header >> BDC BT /F1 12 Tf 72 770 Td (Top of the unturned page) Tj ET 0 0 1 rg 0 760 612 4 re f EMC
                """),
            [5] = CraftedPdf.Helvetica
        });
        using var document = PdfiumDocument.Load(pdf);
        var html = document.RenderPageHtml(0, leftOut with { IncludeImage = false });
        await Assert.That(html).DoesNotContain("Side header");
        await Assert.That(html).Contains("Top of the unturned page");
    }

    [Test]
    public async Task LeavesTheCallersPageAlone()
    {
        using var document = PdfiumDocument.Load(HtmlTests.SinglePage($"{header} {body} {footer}"));
        using var page = document.LoadPage(0);
        var render = page.Render();
        page.RenderHtml(leftOut);
        await Bytes.AreEqual(page.Render(), render);
        await Assert.That(page.GetText()).Contains("Running header");
    }

    [Test]
    public async Task WholeDocument()
    {
        using var document = PdfiumDocument.Load(HtmlTests.SinglePage($"{header} {body} {footer}"));
        var html = document.RenderHtml(leftOut with { Text = HtmlTextMode.Visible });
        await Assert.That(html).Contains(">Body text</text>");
        await Assert.That(html).DoesNotContain("Running header");
        await Assert.That(html).DoesNotContain("Page footer");
    }

    static byte[] Image(PdfiumDocument document, HtmlOptions options)
    {
        byte[]? image = null;
        document.RenderPageHtml(
            0,
            options with
            {
                ImageUrl = (_, png) =>
                {
                    image = png;
                    return "page.png";
                }
            });
        return image!;
    }

    static byte[] Pixels(PdfiumDocument document, HtmlOptions options)
    {
        byte[]? pixels = null;
        document.RenderPageHtml(
            0,
            options with
            {
                ImageUrlFromPixels = (_, image) =>
                {
                    pixels = image.Pixels.ToArray();
                    return "page.png";
                }
            });
        return pixels!;
    }

    static byte[] RenderOf(string content, string formContent = "")
    {
        using var document = PdfiumDocument.Load(HtmlTests.SinglePage(content, formContent));
        return document.RenderPage(0);
    }
}
