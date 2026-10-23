/// <summary>
/// The parts of the API the plain-text sample PDFs cannot exercise, run against documents that
/// have them: IRS Form 1040's form fields and widgets, the NASA fact sheet's colour, tagged
/// structure and page labels. See test-assets.md for the documents.
/// </summary>
public class RealDocumentTests
{
    // Each pixel of a page filled with pure red reads back red, green, blue, alpha in that
    // order: a channel swap anywhere between PDFium and the caller would show here, where a
    // comparison of luminance (the PNG snapshots' SSIM) would not see it.
    [Test]
    public async Task ColourChannelsArriveInOrder()
    {
        var pdf = CraftedPdf.Build(new()
        {
            [1] = "<< /Type /Catalog /Pages 2 0 R >>",
            [2] = "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            [3] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Contents 4 0 R >>",
            [4] = CraftedPdf.Stream("1 0 0 rg 0 0 50 100 re f 0 0 1 rg 50 0 50 100 re f")
        });
        using var document = PdfiumDocument.Load(pdf);
        using var rendered = document.RenderPixels(0, 72, PdfiumNative.RenderAnnotations, PdfiumDocument.OpaqueWhite, null);
        var pixels = rendered.Pixels;
        // Left half red, right half blue.
        var left = pixels[..4].ToArray();
        var right = pixels[((rendered.Width - 1) * 4)..(rendered.Width * 4)].ToArray();
        await Bytes.AreEqual(left, [255, 0, 0, 255]);
        await Bytes.AreEqual(right, [0, 0, 255, 255]);
    }

    // The PNG's colour type: 0 for grayscale, 6 for RGBA (byte 25, in the header chunk).
    static byte ColourType(byte[] png) => png[25];

    // The photo page in colour is an RGBA PNG; rendered in grayscale, every pixel is gray, so
    // the encoder writes a grayscale PNG. A black-on-white page cannot tell the two apart.
    [Test]
    public async Task GrayscaleRendersGray()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.nasa_training_for_space_pdf);
        await Assert.That(ColourType(document.RenderPage(0))).IsEqualTo((byte) 6);
        var gray = document.RenderPage(
            0,
            new RenderOptions
            {
                Grayscale = true
            });
        await Assert.That(ColourType(gray)).IsEqualTo((byte) 0);
    }

    [Test]
    public async Task FormType() =>
        await Assert.That(Load("irs").GetFormType()).IsEqualTo(Morph.PDFium.FormType.XfaForeground);

    [Test]
    public async Task FormFields()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.irs_f1040_pdf);
        using var form = document.LoadForm();
        using var page = document.LoadPage(0);
        var fields = form.GetFields(page);
        await Assert.That(fields.Count).IsEqualTo(128);
        await Verify(fields);
    }

    // The form session draws the fields' widgets over the page, which a plain render leaves to
    // their appearance streams.
    [Test]
    public async Task FormRenderDrawsWidgets()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.irs_f1040_pdf);
        using var form = document.LoadForm();
        await Bytes.AreDifferent(form.RenderPage(0), document.RenderPage(0));
    }

    [Test]
    public async Task Annotations()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.irs_f1040_pdf);
        using var page = document.LoadPage(0);
        var annotations = page.GetAnnotations();
        await Assert.That(annotations.Count).IsEqualTo(128);
        await Assert.That(annotations.All(_ => _.Type == PdfAnnotationType.Widget)).IsTrue();
        await Verify(annotations.Take(10));
    }

    [Test]
    public async Task RemoveAnnotation()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.irs_f1040_pdf);
        using var page = document.LoadPage(0);
        var second = page.GetAnnotations()[1];
        page.RemoveAnnotation(0);
        var remaining = page.GetAnnotations();
        await Assert.That(remaining.Count).IsEqualTo(127);
        await Assert.That(remaining[0].Rectangle).IsEqualTo(second.Rectangle);
    }

    [Test]
    public async Task StructureTree()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.nasa_training_for_space_pdf);
        using var page = document.LoadPage(0);
        var tree = page.GetStructureTree();
        await Assert.That(tree.Count).IsGreaterThan(0);
        await Verify(tree);
    }

    [Test]
    public async Task PageLabels()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.nasa_training_for_space_pdf);
        await Assert.That(document.GetPageLabel(0)).IsEqualTo("1");
        await Assert.That(document.GetPageLabel(1)).IsEqualTo("2");
    }

    [Test]
    public async Task DocumentInfoValues()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.irs_f1040_pdf);
        await Assert.That(document.GetPdfVersion()).IsEqualTo("1.7");
        await Assert.That(document.GetPermissions()).IsEqualTo(DocumentPermissions.All);
        await Assert.That(document.GetUserPermissions()).IsEqualTo(DocumentPermissions.All);
        // Not encrypted.
        await Assert.That(document.GetSecurityHandlerRevision()).IsNull();
        await Assert.That(document.GetFileIdentifier()).IsNotNull();
        await Assert.That(document.GetProperties()!["Title"]).IsEqualTo("2025 Form 1040");
    }

    [Test]
    public async Task PageGeometry()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.irs_f1040_pdf);
        using var page = document.LoadPage(0);
        await Assert.That(page.Size).IsEqualTo(new(612, 792));
        await Assert.That(page.GetBoundingBox()).IsEqualTo(new(0, 0, 612, 792));
    }

    [Test]
    public async Task SearchOptions()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.irs_f1040_pdf);
        using var page = document.LoadPage(0);
        var anyCase = page.Search("income");
        var matchCase = page.Search("income", TextSearchOptions.MatchCase);
        var wholeWord = page.Search("income", TextSearchOptions.MatchWholeWord);
        // "Income" in a heading only matches ignoring case; "income" inside a longer word only
        // matches when whole words are not asked for.
        await Assert.That(matchCase.Count).IsLessThan(anyCase.Count);
        await Assert.That(wholeWord.Count).IsLessThanOrEqualTo(anyCase.Count);
        foreach (var match in matchCase)
        {
            await Assert.That(page.GetText(match.CharIndex, match.CharCount)).IsEqualTo("income");
        }
    }

    [Test]
    public async Task TextInRectangleAndCharAtPoint()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.irs_f1040_pdf);
        using var page = document.LoadPage(0);
        var match = page.Search("Filing Status")[0];
        var rectangle = page.GetTextRectangles(match.CharIndex, match.CharCount)[0];
        await Assert.That(page.GetTextInRectangle(rectangle)).Contains("Filing Status");
        var centre = page.GetCharIndexAt((rectangle.Left + rectangle.Right) / 2, (rectangle.Bottom + rectangle.Top) / 2, 1, 1);
        await Assert.That(centre).IsGreaterThanOrEqualTo(match.CharIndex);
        await Assert.That(centre).IsLessThan(match.CharIndex + match.CharCount);
    }

    [Test]
    public async Task SaveWithVersion()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        using var saved = PdfiumDocument.Load(document.Save(pdfVersion: 14));
        await Assert.That(saved.GetPdfVersion()).IsEqualTo("1.4");
    }

    [Test]
    public async Task GenerateContentWritesEdits()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        using var page = document.LoadPage(0);
        var before = page.ObjectCount;
        page.AddRectangle(new(10, 10, 20, 20), new(255, 0, 0, 255));
        page.GenerateContent();
        using var saved = PdfiumDocument.Load(document.Save());
        using var savedPage = saved.LoadPage(0);
        await Assert.That(savedPage.ObjectCount).IsEqualTo(before + 1);
    }

    static PdfiumDocument Load(string name) =>
        name switch
        {
            "irs" => PdfiumDocument.Load(ProjectFiles.irs_f1040_pdf),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
        };
}
