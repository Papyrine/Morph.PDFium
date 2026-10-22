/// <summary>
/// The readme promises the same pixels on every machine and OS for a document whose fonts are
/// embedded. The snapshot tests compare PNGs by SSIM, which would pass a render a few pixels
/// off, so these render the same pages again and compare them byte for byte with the committed
/// snapshots. CI runs them on Windows, Linux and macOS (arm64).
/// </summary>
public class DeterminismTests
{
    public static IEnumerable<(string pdf, int page, string snapshot)> Renders() =>
    [
        ("sample.pdf", 0, "Samples.RenderPage"),
        ("multi-page.pdf", 0, "Samples.RenderPages#page_0001"),
        ("multi-page.pdf", 3, "Samples.RenderPages#page_0004"),
        ("irs-f1040.pdf", 0, "ComplexDocumentTests.RenderPages_name=irs-f1040#page_1"),
        ("nasa-training-for-space.pdf", 0, "ComplexDocumentTests.RenderPages_name=nasa-training-for-space#page_1")
    ];

    [Test]
    [MethodDataSource(nameof(Renders))]
    public async Task MatchesSnapshotExactly(string pdf, int page, string snapshot)
    {
        using var document = PdfiumDocument.Load(pdf);
        var png = document.RenderPage(page, dpi: 96);
        var expected = await File.ReadAllBytesAsync(Path.Combine(ProjectFiles.ProjectDirectory, $"{snapshot}.verified.png"));
        if (!png.AsSpan().SequenceEqual(expected))
        {
            // Beside the snapshot, where CI's upload of received files picks it up.
            await File.WriteAllBytesAsync(Path.Combine(ProjectFiles.ProjectDirectory, $"{snapshot}.exact.received.png"), png);
        }

        await Bytes.AreEqual(png, expected);
    }
}
