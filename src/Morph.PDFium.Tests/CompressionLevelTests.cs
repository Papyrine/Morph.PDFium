// The zlib level can be set per render through RenderOptions or, for renders that take no
// options, process-wide through RenderOptions.DefaultCompressionLevel.
public class CompressionLevelTests
{
    [Test]
    public async Task PerRenderLevelChangesSize()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.multi_page_pdf);
        var fastest = document.RenderPage(0, new RenderOptions { CompressionLevel = PngCompressionLevel.Fastest });
        var smallest = document.RenderPage(0, new RenderOptions { CompressionLevel = PngCompressionLevel.SmallestSize });
        await Assert.That(fastest.Length).IsGreaterThan(smallest.Length);
    }

    // Changes process-wide state, so nothing else may render while it runs.
    [Test]
    [NotInParallel]
    public async Task GlobalLevelAppliesToRendersWithoutOptions()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.multi_page_pdf);
        using var page = document.LoadPage(0);
        var standard = document.RenderPage(0);
        var original = RenderOptions.DefaultCompressionLevel;
        try
        {
            RenderOptions.DefaultCompressionLevel = PngCompressionLevel.Fastest;
            var expected = document.RenderPage(0, new RenderOptions { CompressionLevel = PngCompressionLevel.Fastest });
            await Assert.That(expected.Length).IsGreaterThan(standard.Length);
            await Assert.That(document.RenderPage(0)).IsEquivalentTo(expected);
            await Assert.That(document.RenderPages()[0]).IsEquivalentTo(expected);
            await Assert.That(page.Render()).IsEquivalentTo(expected);
            // Options created from now on start from the new default.
            await Assert.That(new RenderOptions().CompressionLevel).IsEqualTo(PngCompressionLevel.Fastest);
        }
        finally
        {
            RenderOptions.DefaultCompressionLevel = original;
        }
    }

    [Test]
    public async Task DefaultIsZlibDefault() =>
        await Assert.That(RenderOptions.DefaultCompressionLevel).IsEqualTo(PngCompressionLevel.Optimal);

    [Test]
    public async Task InvalidLevelsThrow()
    {
        await Assert.That(() => RenderOptions.DefaultCompressionLevel = (PngCompressionLevel) 10).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => RenderOptions.DefaultCompressionLevel = (PngCompressionLevel) (-1)).Throws<ArgumentOutOfRangeException>();

        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        using var page = document.LoadPage(0);
        using var form = document.LoadForm();
        var options = new RenderOptions { CompressionLevel = (PngCompressionLevel) 10 };
        await Assert.That(() => document.RenderPage(0, options)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => document.RenderRegion(0, new(0, 0, 100, 100), options)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => page.Render(options)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => page.RenderRegion(new(0, 0, 100, 100), options)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => form.RenderPage(0, options)).Throws<ArgumentOutOfRangeException>();
    }
}
