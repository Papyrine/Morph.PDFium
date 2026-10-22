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
        await browser.DisposeAsync();
        playwright.Dispose();
    }

    public static IEnumerable<string> Pages() =>
        ["sample", "multi-page 1", "multi-page 4", "links", "rotated"];

    static (PdfiumDocument Document, int Page) Load(string name) =>
        name switch
        {
            "sample" => (PdfiumDocument.Load(ProjectFiles.sample_pdf), 0),
            "multi-page 1" => (PdfiumDocument.Load(ProjectFiles.multi_page_pdf), 0),
            "multi-page 4" => (PdfiumDocument.Load(ProjectFiles.multi_page_pdf), 3),
            "links" => (PdfiumDocument.Load(HtmlTests.PdfWithLinks()), 0),
            "rotated" => (PdfiumDocument.Load(HtmlTests.RotatedPdf()), 0),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
        };

    // The overlay page is its image with transparent text and links over it, so the browser
    // should show exactly the PNG PDFium rendered: same size, and the same pixels bar the odd
    // one where Chromium blends differently.
    [Test]
    [MethodDataSource(nameof(Pages))]
    public async Task OverlayLooksLikeTheRender(string name)
    {
        var (document, index) = Load(name);
        using var _ = document;
        var screenshot = await Screenshot(document.RenderPageHtml(index), 1);
        var comparison = await Compare(screenshot, document.RenderPage(index));
        await Assert.That(comparison.SameSize).IsTrue();
        await Assert.That(comparison.DifferingFraction).IsLessThan(0.001);
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
        var comparison = await Compare(screenshot, document.RenderPage(0, render));
        await Assert.That(comparison.SameSize).IsTrue();
        await Assert.That(comparison.DifferingFraction).IsLessThan(0.001);
    }

    // Visible text is drawn in the browser's fonts, so its pixels differ from PDFium's, but
    // each run should cover the same stretch of the page. Blurring both away from the shapes
    // of the glyphs and correlating where the ink lies checks that.
    [Test]
    [MethodDataSource(nameof(Pages))]
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
        var comparison = await Compare(screenshot, document.RenderPage(index));
        await Assert.That(comparison.SameSize).IsTrue();
        await Assert.That(comparison.InkCorrelation).IsGreaterThan(0.85);
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
        var comparison = await Compare(screenshot, document.RenderPage(0));
        await Assert.That(comparison.InkCorrelation).IsLessThan(0.6);
    }

    static async Task<byte[]> Screenshot(string fragment, float deviceScale)
    {
        await using var context = await browser.NewContextAsync(
            new()
            {
                ViewportSize = new()
                {
                    Width = 1400,
                    Height = 1400
                },
                DeviceScaleFactor = deviceScale
            });
        var page = await context.NewPageAsync();
        await page.SetContentAsync($"<!DOCTYPE html><html><body style=\"margin:0\">{fragment}</body></html>");
        await page.EvaluateAsync("document.fonts.ready");
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
        async ([first, second]) => {
          const load = source => new Promise((resolve, reject) => {
            const image = new Image();
            image.onload = () => resolve(image);
            image.onerror = reject;
            image.src = 'data:image/png;base64,' + source;
          });
          const [a, b] = await Promise.all([load(first), load(second)]);
          const sizes = `${a.width}x${a.height} vs ${b.width}x${b.height}`;
          // A page whose size is not a whole number of pixels is rounded up by the browser's
          // screenshot and to the nearest pixel by PDFium, so allow a pixel either way and
          // compare the area both cover.
          if (Math.abs(a.width - b.width) > 1 || Math.abs(a.height - b.height) > 1) {
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

          // A pixel differs when any channel is off by more than an eighth.
          let differing = 0;
          for (let i = 0; i < pa.length; i += 4) {
            if (Math.abs(pa[i] - pb[i]) > 32 || Math.abs(pa[i + 1] - pb[i + 1]) > 32 || Math.abs(pa[i + 2] - pb[i + 2]) > 32) {
              differing++;
            }
          }

          // Ink is darkness, blurred by a box a few pixels across each way.
          const radius = 3;
          const ink = data => {
            const values = new Float64Array(width * height);
            for (let i = 0; i < values.length; i++) {
              values[i] = 1 - (data[i * 4] * 0.299 + data[i * 4 + 1] * 0.587 + data[i * 4 + 2] * 0.114) / 255;
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

    static async Task<Comparison> Compare(byte[] screenshot, byte[] render)
    {
        await using var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();
        var result = await page.EvaluateAsync<Comparison>(compareScript, new[] { Convert.ToBase64String(screenshot), Convert.ToBase64String(render) });
        // Shown with the test's output, to see how close a page came.
        Console.WriteLine(result);
        return result;
    }
}
