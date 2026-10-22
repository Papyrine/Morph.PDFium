using Microsoft.Playwright;

/// <summary>
/// Renders the HTML in headless Chromium and compares the screenshot with PDFium's PNG of the
/// same page. The browser also decodes both PNGs and does the comparing, so no image library
/// is needed.
/// </summary>
public class HtmlBrowserTests
{
    static IPlaywright playwright = null!;
    static IBrowser browser = null!;

    [Before(HookType.Class)]
    public static async Task Launch()
    {
        // Downloads Chromium the first time; a no-op once it is installed.
        var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
        if (exitCode != 0)
        {
            throw new($"Installing Chromium for Playwright failed with exit code {exitCode}");
        }

        playwright = await Playwright.CreateAsync();
        browser = await playwright.Chromium.LaunchAsync();
    }

    [After(HookType.Class)]
    public static async Task Close()
    {
        // Either may be missing when Launch failed; that failure is the one to see.
        if (browser is not null)
        {
            await browser.DisposeAsync();
        }

        playwright?.Dispose();
    }

    public static IEnumerable<string> Pages() =>
        [.. TextPages(), "irs 1", "irs 2", "nasa 1"];

    // Pages that are almost all text, so text-only HTML should look like them too. Page 1 of
    // the NASA fact sheet is a third photo, and the IRS form is as much ruled lines, boxes and
    // shading as text, all of which text-only HTML leaves out.
    public static IEnumerable<string> TextPages() =>
        ["sample", "multi-page 1", "multi-page 4", "links", "rotated", "scaled", "stroked", "hebrew", "mirrored", "nasa 2"];

    static (PdfiumDocument Document, int Page) Load(string name) =>
        name switch
        {
            "sample" => (PdfiumDocument.Load(ProjectFiles.sample_pdf), 0),
            "multi-page 1" => (PdfiumDocument.Load(ProjectFiles.multi_page_pdf), 0),
            "multi-page 4" => (PdfiumDocument.Load(ProjectFiles.multi_page_pdf), 3),
            "links" => (PdfiumDocument.Load(HtmlTests.PdfWithLinks()), 0),
            "rotated" => (PdfiumDocument.Load(HtmlTests.RotatedPdf()), 0),
            "scaled" => (PdfiumDocument.Load(HtmlTests.ScaledTextPdf()), 0),
            "stroked" => (PdfiumDocument.Load(HtmlTests.StrokedTextPdf()), 0),
            "hebrew" => (PdfiumDocument.Load(HtmlTests.HebrewPdf("BT /F1 20 Tf 72 700 Td (abcd abcd dcba.) Tj ET")), 0),
            "mirrored" => (PdfiumDocument.Load(HtmlTests.SinglePage("BT /F1 24 Tf -1 0 0 1 300 700 Tm (Mirrored text) Tj ET BT /F1 24 Tf 1 0 0 -1 72 600 Tm (Upside down) Tj ET")), 0),
            "irs 1" => (PdfiumDocument.Load(ProjectFiles.irs_f1040_pdf), 0),
            "irs 2" => (PdfiumDocument.Load(ProjectFiles.irs_f1040_pdf), 1),
            "nasa 1" => (PdfiumDocument.Load(ProjectFiles.nasa_training_for_space_pdf), 0),
            "nasa 2" => (PdfiumDocument.Load(ProjectFiles.nasa_training_for_space_pdf), 1),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
        };

    // The overlay page is its image with transparent text and links over it, so the browser
    // should show exactly the PNG PDFium rendered: the same size, and the same pixels.
    [Test]
    [MethodDataSource(nameof(Pages))]
    public async Task OverlayLooksLikeTheRender(string name)
    {
        var (document, index) = Load(name);
        using var _ = document;
        var screenshot = await Screenshot(document.RenderPageHtml(index), 1);
        var comparison = await Compare(screenshot, document.RenderPage(index), IsFractional(document, index, 96));
        await Assert.That(comparison.SameSize).IsTrue();
        await Assert.That(comparison.DifferingFraction).IsEqualTo(0);
    }

    // At twice the resolution, in a browser at twice the device scale, the page is still the
    // same size in CSS pixels, so the larger image fills it pixel for pixel.
    [Test]
    public async Task HighResolutionOverlayLooksLikeTheRender()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        var render = new RenderOptions
        {
            Dpi = 192
        };
        var screenshot = await Screenshot(
            document.RenderPageHtml(
                0,
                new()
                {
                    Render = render
                }),
            2);
        var comparison = await Compare(screenshot, document.RenderPage(0, render), IsFractional(document, 0, 192));
        await Assert.That(comparison.SameSize).IsTrue();
        await Assert.That(comparison.DifferingFraction).IsEqualTo(0);
    }

    // Visible text is drawn in the browser's fonts, so its pixels differ from PDFium's, but
    // each run should cover the same stretch of the page. Blurring both away from the shapes
    // of the glyphs and correlating where the ink lies checks that.
    [Test]
    [MethodDataSource(nameof(TextPages))]
    public async Task VisibleTextLiesWhereTheRenderedTextDoes(string name)
    {
        var (document, index) = Load(name);
        using var _ = document;
        var html = document.RenderPageHtml(
            index,
            new()
            {
                IncludeImage = false,
                Text = HtmlTextMode.Visible
            });
        var screenshot = await Screenshot(html, 1);
        var comparison = await Compare(screenshot, document.RenderPage(index), IsFractional(document, index, 96));
        await Assert.That(comparison.SameSize).IsTrue();
        await Assert.That(comparison.InkCorrelation).IsGreaterThan(0.85);
    }

    // Visible text over the image rendered without it: the images and drawings are PDFium's
    // own pixels, and the text is in the browser's fonts. Both images share the background, so
    // it is taken out of both before comparing, which leaves the text alone: on the NASA photo
    // page the background by itself correlates 0.96 with the render.
    [Test]
    [MethodDataSource(nameof(Pages))]
    public async Task VisibleTextOverTheBackgroundLooksLikeTheRender(string name)
    {
        var (document, index) = Load(name);
        using var _ = document;
        var html = document.RenderPageHtml(
            index,
            new()
            {
                Text = HtmlTextMode.Visible
            });
        var screenshot = await Screenshot(html, 1);
        byte[] background;
        using (var page = document.LoadPage(index))
        {
            background = page.RenderWithoutText(new(), false);
        }

        var comparison = await Compare(screenshot, document.RenderPage(index), IsFractional(document, index, 96), background);
        await Assert.That(comparison.SameSize).IsTrue();
        await Assert.That(comparison.InkCorrelation).IsGreaterThan(0.85);
    }

    // Taking out the background must leave the comparison resting on the text: the NASA photo
    // page with its text hidden, which would score 0.96 against the whole render, must fail.
    [Test]
    public async Task TextInkOverTheBackgroundDetectsMissingText()
    {
        var (document, index) = Load("nasa 1");
        using var _ = document;
        var html = document.RenderPageHtml(
            index,
            new()
            {
                Text = HtmlTextMode.Visible
            });
        var hidden = html.Replace("<svg ", "<svg visibility=\"hidden\" ");
        byte[] background;
        using (var page = document.LoadPage(index))
        {
            background = page.RenderWithoutText(new(), false);
        }

        var comparison = await Compare(await Screenshot(hidden, 1), document.RenderPage(index), false, background);
        await Assert.That(comparison.InkCorrelation > 0.85).IsFalse();
    }

    // The correlation must tell placement apart: the same text moved half a line down
    // should score well below the threshold above.
    [Test]
    public async Task InkCorrelationDetectsMisplacedText()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.multi_page_pdf);
        var html = document.RenderPageHtml(
            0,
            new()
            {
                IncludeImage = false,
                Text = HtmlTextMode.Visible
            });
        var shifted = html.Replace("<svg ", "<svg transform=\"translate(0 7)\" ");
        var screenshot = await Screenshot(shifted, 1);
        var comparison = await Compare(screenshot, document.RenderPage(0), false);
        await Assert.That(comparison.InkCorrelation).IsLessThan(0.6);
    }

    // Printing the HTML to PDF in Chromium, honouring its @page rules, and reading that PDF back
    // gives one sheet per page, each the size of its page, with no blank sheet between or after.
    [Test]
    public async Task PrintsEachPageOnASheetOfItsSize()
    {
        using var document = HtmlTests.MixedSizeDocument();
        using var printed = PdfiumDocument.Load(await Print(document));
        await Assert.That(printed.PageCount).IsEqualTo(document.PageCount);
        for (var index = 0; index < document.PageCount; index++)
        {
            var expected = document.GetPageSize(index);
            var actual = printed.GetPageSize(index);
            await Assert.That(Math.Abs(actual.Width - expected.Width)).IsLessThan(1);
            await Assert.That(Math.Abs(actual.Height - expected.Height)).IsLessThan(1);
        }
    }

    // Each printed sheet should look like the page: Chromium prints the page image, and
    // PDFium renders the printed sheet back at the same resolution. The image is resampled
    // on the way through, so this compares where the ink lies rather than exact pixels.
    [Test]
    [Arguments("multi-page 1")]
    [Arguments("rotated")]
    public async Task PrintedPagesLookLikeThePages(string name)
    {
        var (document, _) = Load(name);
        using var _ = document;
        using var printed = PdfiumDocument.Load(await Print(document));
        await Assert.That(printed.PageCount).IsEqualTo(document.PageCount);
        for (var index = 0; index < document.PageCount; index++)
        {
            var comparison = await Compare(printed.RenderPage(index), document.RenderPage(index), true);
            await Assert.That(comparison.SameSize).IsTrue();
            await Assert.That(comparison.InkCorrelation).IsGreaterThan(0.95);
        }
    }

    static async Task<byte[]> Print(PdfiumDocument document)
    {
        var html = document.RenderHtml(
            new()
            {
                Render = new()
                {
                    Dpi = 150
                },
                LazyLoadImages = false
            });
        await using var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.SetContentAsync(html);
        return await page.PdfAsync(
            new()
            {
                PreferCSSPageSize = true,
                PrintBackground = true
            });
    }

    // A folder of PDFs, such as another converter's test files, to run through the browser
    // comparison without committing them. Off unless the variable is set.
    const string corpusVariable = "MORPH_PDFIUM_HTML_CORPUS";
    // Where the corpus test writes each page's PNG and HTML for a look, when set.
    const string corpusOutputVariable = "MORPH_PDFIUM_HTML_CORPUS_OUTPUT";

    public static IEnumerable<string> CorpusFiles()
    {
        var folder = Environment.GetEnvironmentVariable(corpusVariable);
        if (string.IsNullOrEmpty(folder))
        {
            return [];
        }

        return Directory.EnumerateFiles(folder, "*.pdf", SearchOption.AllDirectories).Order();
    }

    // Every page of every PDF in the corpus folder. The overlay must match PDFium's render, as
    // it does for the files above. Visible text, alone and over the text-free background, is
    // only reported, not checked: text-only HTML lacks the page's images and drawings, and in
    // both the browser's fonts differ from the PDF's, so how well they correlate depends on
    // the page.
    [Test]
    [MethodDataSource(nameof(CorpusFiles), SkipIfEmpty = true)]
    public async Task Corpus(string path)
    {
        using var document = PdfiumDocument.Load(path);
        var output = Environment.GetEnvironmentVariable(corpusOutputVariable);
        var failures = new List<string>();
        for (var index = 0; index < document.PageCount; index++)
        {
            var render = document.RenderPage(index);
            var overlayHtml = document.RenderPageHtml(index);
            var visibleHtml = document.RenderPageHtml(
                index,
                new()
                {
                    IncludeImage = false,
                    Text = HtmlTextMode.Visible
                });
            var backgroundHtml = document.RenderPageHtml(
                index,
                new()
                {
                    Text = HtmlTextMode.Visible
                });
            var slack = IsFractional(document, index, 96);
            var overlay = await Compare(await Screenshot(overlayHtml, 1), render, slack);
            var visible = await Compare(await Screenshot(visibleHtml, 1), render, slack);
            var background = await Compare(await Screenshot(backgroundHtml, 1), render, slack);
            Console.WriteLine($"Page {index + 1}: overlay {overlay}; ink correlation of visible text {visible.InkCorrelation:0.####}, over the background {background.InkCorrelation:0.####}");
            if (!overlay.SameSize || overlay.DifferingFraction >= 0.001)
            {
                failures.Add($"page {index + 1}: {overlay}");
            }

            if (!string.IsNullOrEmpty(output))
            {
                var name = $"{Path.GetFileNameWithoutExtension(path)}-{index + 1}";
                Directory.CreateDirectory(output);
                await File.WriteAllBytesAsync(Path.Combine(output, $"{name}.png"), render);
                await File.WriteAllTextAsync(Path.Combine(output, $"{name}-overlay.html"), Wrap(overlayHtml));
                await File.WriteAllTextAsync(Path.Combine(output, $"{name}-visible.html"), Wrap(visibleHtml));
                await File.WriteAllTextAsync(Path.Combine(output, $"{name}-background.html"), Wrap(backgroundHtml));
            }
        }

        await Assert.That(failures).IsEmpty();
    }

    // The charset matters for the corpus output files, which a browser would otherwise read
    // as Windows-1252.
    static string Wrap(string fragment) =>
        $"<!DOCTYPE html><html><head><meta charset=\"utf-8\"></head><body style=\"margin:0\">{fragment}</body></html>";

    static async Task<byte[]> Screenshot(string fragment, float deviceScale)
    {
        await using var context = await browser.NewContextAsync(
            new()
            {
                // Wider than any page likely to be compared, since the page shrinks to fit a
                // narrower viewport (max-width:100%), and a shrunk page would not match its render.
                ViewportSize = new()
                {
                    Width = 4000,
                    Height = 1400
                },
                DeviceScaleFactor = deviceScale
            });
        var page = await context.NewPageAsync();
        await page.SetContentAsync(Wrap(fragment));
        // A lazily loaded image off screen would be captured before it arrives.
        await page.EvaluateAsync(
            """
            async () => {
              const images = [...document.images];
              images.forEach(_ => _.loading = 'eager');
              await Promise.all(images.map(_ => _.decode()));
              await document.fonts.ready;
            }
            """);
        return await page.Locator(".pdf-page").ScreenshotAsync();
    }

    // Playwright fills the result in through settable properties.
    class Comparison
    {
        public bool SameSize { get; set; }
        public double DifferingFraction { get; set; }
        public double InkCorrelation { get; set; }
        public string? Sizes { get; set; }

        public override string ToString() =>
            $"SameSize: {SameSize}, DifferingFraction: {DifferingFraction:0.######}, InkCorrelation: {InkCorrelation:0.####} {Sizes}";
    }

    const string compareScript =
        """
        async ([first, second, background, slack]) => {
          const load = source => new Promise((resolve, reject) => {
            const image = new Image();
            image.onload = () => resolve(image);
            image.onerror = reject;
            image.src = 'data:image/png;base64,' + source;
          });
          const [a, b] = await Promise.all([load(first), load(second)]);
          const sizes = `${a.width}x${a.height} vs ${b.width}x${b.height}`;
          // A page whose size is not a whole number of pixels is rounded up by the browser's
          // screenshot and to the nearest pixel by PDFium, so for one of those, and only those,
          // allow a pixel either way and compare the area both cover.
          const allowed = slack ? 1 : 0;
          if (Math.abs(a.width - b.width) > allowed || Math.abs(a.height - b.height) > allowed) {
            return { sameSize: false, differingFraction: 1, inkCorrelation: 0, sizes };
          }

          const width = Math.min(a.width, b.width);
          const height = Math.min(a.height, b.height);
          const pixels = image => {
            const canvas = document.createElement('canvas');
            canvas.width = width;
            canvas.height = height;
            const context = canvas.getContext('2d');
            context.drawImage(image, 0, 0);
            return context.getImageData(0, 0, width, height).data;
          };
          const pa = pixels(a);
          const pb = pixels(b);
          const pc = background ? pixels(await load(background)) : null;

          // A pixel differs when any channel is off by more than rounding.
          let differing = 0;
          for (let i = 0; i < pa.length; i += 4) {
            if (Math.abs(pa[i] - pb[i]) > 2 || Math.abs(pa[i + 1] - pb[i + 1]) > 2 || Math.abs(pa[i + 2] - pb[i + 2]) > 2) {
              differing++;
            }
          }

          // Ink is darkness, blurred by a box a few pixels across each way. Given the page's
          // background, only what lies over it counts: the text, without the images and
          // drawings both images share, which would otherwise correlate on their own.
          const radius = 3;
          const darkness = (data, i) => 1 - (data[i * 4] * 0.299 + data[i * 4 + 1] * 0.587 + data[i * 4 + 2] * 0.114) / 255;
          const ink = data => {
            const values = new Float64Array(width * height);
            for (let i = 0; i < values.length; i++) {
              values[i] = darkness(data, i) - (pc ? darkness(pc, i) : 0);
            }

            const blurred = new Float64Array(values.length);
            for (let y = 0; y < height; y++) {
              let sum = 0;
              for (let x = -radius; x < width + radius; x++) {
                if (x + radius < width) sum += values[y * width + x + radius];
                if (x - radius - 1 >= 0) sum -= values[y * width + x - radius - 1];
                if (x >= 0 && x < width) blurred[y * width + x] = sum;
              }
            }

            const result = new Float64Array(values.length);
            for (let x = 0; x < width; x++) {
              let sum = 0;
              for (let y = -radius; y < height + radius; y++) {
                if (y + radius < height) sum += blurred[(y + radius) * width + x];
                if (y - radius - 1 >= 0) sum -= blurred[(y - radius - 1) * width + x];
                if (y >= 0 && y < height) result[y * width + x] = sum;
              }
            }

            return result;
          };
          const ia = ink(pa);
          const ib = ink(pb);
          let meanA = 0, meanB = 0;
          for (let i = 0; i < ia.length; i++) {
            meanA += ia[i];
            meanB += ib[i];
          }
          meanA /= ia.length;
          meanB /= ib.length;
          let covariance = 0, varianceA = 0, varianceB = 0;
          for (let i = 0; i < ia.length; i++) {
            const da = ia[i] - meanA;
            const db = ib[i] - meanB;
            covariance += da * db;
            varianceA += da * da;
            varianceB += db * db;
          }

          return {
            sameSize: true,
            differingFraction: differing / (width * height),
            inkCorrelation: covariance / Math.sqrt(varianceA * varianceB),
            sizes
          };
        }
        """;

    // Whether the page's size at this resolution is a fractional number of pixels, which the
    // browser and PDFium round differently.
    static bool IsFractional(PdfiumDocument document, int index, double dpi)
    {
        var size = document.GetPageSize(index);
        var width = size.Width * dpi / 72;
        var height = size.Height * dpi / 72;
        return Math.Abs(width - Math.Round(width)) > 1e-6 || Math.Abs(height - Math.Round(height)) > 1e-6;
    }

    static async Task<Comparison> Compare(byte[] screenshot, byte[] render, bool slack, byte[]? background = null)
    {
        await using var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();
        object?[] arguments = [Convert.ToBase64String(screenshot), Convert.ToBase64String(render), background is null ? null : Convert.ToBase64String(background), slack];
        var result = await page.EvaluateAsync<Comparison>(compareScript, arguments);
        // Shown with the test's output, to see how close a page came.
        Console.WriteLine(result);
        return result;
    }
}
