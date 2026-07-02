public class FileIdTests
{
    static byte[] Ramp(int seed)
    {
        var bytes = new byte[16];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte) (seed + i);
        }

        return bytes;
    }

    [Test]
    public async Task SetFileIdentifierRoundTrips()
    {
        var permanent = Ramp(1);
        var changing = Ramp(200);

        byte[] bytes;
        using (var document = PdfiumDocument.Load("sample.pdf"))
        {
            document.SetFileIdentifier(permanent, changing);
            bytes = document.Save();
        }

        using var reloaded = PdfiumDocument.Load(bytes);
        // The appended incremental trailer must leave a structurally valid, renderable document.
        await Assert.That(reloaded.PageCount).IsEqualTo(1);
        await Assert.That(reloaded.RenderPage(0).Length).IsGreaterThan(0);
        await Assert.That(reloaded.GetFileIdentifier(FileIdentifierType.Permanent)).IsEqualTo(Convert.ToHexStringLower(permanent));
        await Assert.That(reloaded.GetFileIdentifier(FileIdentifierType.Changing)).IsEqualTo(Convert.ToHexStringLower(changing));
    }

    [Test]
    public async Task SingleArgumentSetsBothElements()
    {
        var id = Ramp(9);
        using var document = PdfiumDocument.Load("sample.pdf");
        document.SetFileIdentifier(id);

        using var reloaded = PdfiumDocument.Load(document.Save());
        var expected = Convert.ToHexStringLower(id);
        await Assert.That(reloaded.GetFileIdentifier(FileIdentifierType.Permanent)).IsEqualTo(expected);
        await Assert.That(reloaded.GetFileIdentifier(FileIdentifierType.Changing)).IsEqualTo(expected);
    }

    [Test]
    public async Task IdentifierIsStableAcrossSaves()
    {
        var id = Ramp(42);
        using var document = PdfiumDocument.Load("sample.pdf");
        document.SetFileIdentifier(id);

        using var first = PdfiumDocument.Load(document.Save());
        using var second = PdfiumDocument.Load(document.Save());
        var expected = Convert.ToHexStringLower(id);
        await Assert.That(first.GetFileIdentifier(FileIdentifierType.Changing)).IsEqualTo(expected);
        await Assert.That(second.GetFileIdentifier(FileIdentifierType.Changing)).IsEqualTo(expected);
    }

    [Test]
    public async Task ValidatesArguments()
    {
        using var document = PdfiumDocument.Load("sample.pdf");
        await Assert.That(() => document.SetFileIdentifier(null!)).Throws<ArgumentNullException>();
        await Assert.That(() => document.SetFileIdentifier([])).Throws<ArgumentException>();
        await Assert.That(() => document.SetFileIdentifier(Ramp(1), [])).Throws<ArgumentException>();
    }
}
