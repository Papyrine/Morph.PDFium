public class HtmlTests
{
    static readonly HtmlOptions textOnly = new()
    {
        IncludeImage = false,
        Text = HtmlTextMode.Visible
    };

    [Test]
    public async Task PageFragment()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        await Verify(document.RenderPageHtml(0, textOnly), "html")
            .Snapshot(
                """
                <div class="pdf-page" id="page-1" style="position:relative;overflow:hidden;width:612pt;max-width:100%;aspect-ratio:612/792;background:#ffffff;page:pdf-612x792">
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 612 792" preserveAspectRatio="none" xml:space="preserve" style="position:absolute;left:0;top:0;width:100%;height:100%;white-space:pre" fill="#000">
                <text x="72" y="82.33" font-size="11" textLength="190.76" lengthAdjust="spacingAndGlyphs" font-family="'Aptos', sans-serif">Hello, World! This is a simple paragraph.</text>
                </svg>
                </div>

                """);
    }

    [Test]
    public async Task OverlayFragment()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        string? image = null;
        var html = document.RenderPageHtml(
            0,
            new()
            {
                ImageUrl = (_, png) =>
                {
                    image = Convert.ToBase64String(png);
                    return "page.png";
                }
            });
        await Assert.That(image).IsNotNull();
        await Verify(html, "html")
            .Snapshot(
                """
                <div class="pdf-page" id="page-1" style="position:relative;overflow:hidden;width:612pt;max-width:100%;aspect-ratio:612/792;background:#ffffff;page:pdf-612x792">
                <img src="page.png" alt="" draggable="false" style="position:absolute;left:0;top:0;width:100%;height:100%;user-select:none">
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 612 792" preserveAspectRatio="none" xml:space="preserve" style="position:absolute;left:0;top:0;width:100%;height:100%;white-space:pre" class="pdf-overlay" fill="transparent" font-family="sans-serif">
                <text x="72" y="82.33" font-size="11" textLength="190.76" lengthAdjust="spacingAndGlyphs">Hello, World! This is a simple paragraph.</text>
                </svg>
                </div>

                """);
    }

    [Test]
    public async Task Document()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.multi_page_pdf);
        await Verify(document.RenderHtml(textOnly), "html");
    }

    [Test]
    public async Task ImageIsThePageRender()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        var options = new HtmlOptions
        {
            Render = new()
            {
                Dpi = 72
            }
        };
        var html = document.RenderPageHtml(0, options);
        var expected = "data:image/png;base64," + Convert.ToBase64String(document.RenderPage(0, options.Render));
        await Assert.That(html).Contains($"<img src=\"{expected}\" alt=\"\"");
    }

    [Test]
    public async Task ImageUrlIsGivenEachPage()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.multi_page_pdf);
        var pages = new System.Collections.Concurrent.ConcurrentDictionary<int, byte[]>();
        var html = document.RenderHtml(
            new()
            {
                ImageUrl = (index, png) =>
                {
                    pages[index] = png;
                    return $"images/{index + 1}.png";
                }
            });
        await Assert.That(pages.Keys.Order()).IsEquivalentTo([0, 1, 2, 3]);
        for (var index = 0; index < 4; index++)
        {
            await Assert.That(html).Contains($"<img src=\"images/{index + 1}.png\"");
            await Assert.That(pages[index]).IsEquivalentTo(document.RenderPage(index));
        }

        // The pages come out in order.
        await Assert.That(html.IndexOf("images/1.png", StringComparison.Ordinal)).IsLessThan(html.IndexOf("images/4.png", StringComparison.Ordinal));
    }

    // Pages of three sizes, portrait, landscape and fractional, the first size twice, each
    // labelled with its size.
    internal static PdfiumDocument MixedSizeDocument()
    {
        var document = PdfiumDocument.CreateNew();
        (double Width, double Height)[] sizes = [(612, 792), (842, 595), (612, 792), (300.5, 400)];
        for (var index = 0; index < sizes.Length; index++)
        {
            var (width, height) = sizes[index];
            using var page = document.NewPage(index, width, height);
            page.AddText($"Page {index + 1}: {width} x {height}", 36, height - 72, fontSize: 18);
        }

        return document;
    }

    [Test]
    public async Task PrintsEachPageOnASheetOfItsSize()
    {
        using var document = MixedSizeDocument();
        var html = document.RenderHtml(textOnly);
        // One rule per distinct size, the repeated size only once.
        await Assert.That(html).Contains("@page pdf-612x792{size:612pt 792pt}\n@page pdf-842x595{size:842pt 595pt}\n@page pdf-300_5x400{size:300.5pt 400pt}\n</style>");
        await Assert.That(html.Split("@page pdf-612x792{").Length - 1).IsEqualTo(1);
        // Each page asks for the rule of its size.
        await Assert.That(html).Contains("id=\"page-2\" style=\"position:relative;overflow:hidden;width:842pt;max-width:100%;aspect-ratio:842/595;background:#ffffff;page:pdf-842x595\"");
        await Assert.That(html).Contains("id=\"page-4\" style=\"position:relative;overflow:hidden;width:300.5pt;max-width:100%;aspect-ratio:300.5/400;background:#ffffff;page:pdf-300_5x400\"");
    }

    // A one page PDF of the given content stream, with Helvetica as /F1 and a form XObject
    // /Fm1 that draws the given form content.
    static byte[] SinglePage(string content, string formContent = "") =>
        CraftedPdf.Build(new()
        {
            [1] = "<< /Type /Catalog /Pages 2 0 R >>",
            [2] = "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            [3] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> /XObject << /Fm1 6 0 R >> >> >>",
            [4] = CraftedPdf.Stream(content),
            [5] = CraftedPdf.Helvetica,
            [6] = CraftedPdf.Stream(formContent, "/Type /XObject /Subtype /Form /BBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >>")
        });

    // The page image the HTML gets with visible text over it.
    static byte[] Background(PdfiumDocument document)
    {
        byte[]? image = null;
        document.RenderPageHtml(
            0,
            new()
            {
                Text = HtmlTextMode.Visible,
                ImageUrl = (_, png) =>
                {
                    image = png;
                    return "page.png";
                }
            });
        return image!;
    }

    // PDFium's own render of the same page with the text taken out of its content: what the
    // background should be, pixel for pixel, so byte for byte as a PNG.
    static byte[] RenderOf(string content, string formContent = "")
    {
        using var document = PdfiumDocument.Load(SinglePage(content, formContent));
        return document.RenderPage(0);
    }

    [Test]
    public async Task BackgroundLeavesOutTheText()
    {
        const string drawing = "1 0 0 rg 72 500 200 100 re f 0 0 1 RG 4 w 300 300 m 500 450 l S";
        using var document = PdfiumDocument.Load(SinglePage($"{drawing} 0 g BT /F1 24 Tf 72 700 Td (Some text) Tj ET BT /F1 12 Tf 1 Tr 72 650 Td (Outlined) Tj ET"));
        var background = Background(document);
        await Assert.That(background).IsEquivalentTo(RenderOf(drawing));
        await Assert.That(background).IsNotEquivalentTo(document.RenderPage(0));
    }

    [Test]
    public async Task BackgroundLeavesOutTextInForms()
    {
        using var document = PdfiumDocument.Load(SinglePage("q 1 0 0 1 0 -100 cm /Fm1 Do Q", "0 1 0 rg 72 500 200 100 re f BT /F1 24 Tf 72 700 Td (In a form) Tj ET"));
        await Assert.That(Background(document)).IsEquivalentTo(RenderOf("q 1 0 0 1 0 -100 cm /Fm1 Do Q", "0 1 0 rg 72 500 200 100 re f"));
    }

    [Test]
    public async Task BackgroundKeepsTextClipping()
    {
        // Mode 4 draws the text and clips what follows to its glyphs; left out of the
        // background, the text must still clip, as mode 7 does without drawing.
        const string clipped = "q BT /F1 120 Tf {0} Tr 72 500 Td (CLIP) Tj ET 0 0 1 rg 0 0 612 792 re f Q";
        using var document = PdfiumDocument.Load(SinglePage(string.Format(clipped, 4)));
        await Assert.That(Background(document)).IsEquivalentTo(RenderOf(string.Format(clipped, 7)));
    }

    // Every kind of text the background leaves out: over a drawing, outlined, inside a form,
    // and clipping a fill, which must still be clipped.
    static byte[] BackgroundPdf() =>
        SinglePage(
            """
            1 0 0 rg 60 600 300 140 re f 0 0 1 RG 4 w 380 560 m 560 740 l S
            0 g BT /F1 28 Tf 72 690 Td (Text over a drawing) Tj ET
            BT /F1 36 Tf 1 Tr 0 0 1 RG 1 w 72 520 Td (Outlined text) Tj ET
            q 1 0 0 1 0 -300 cm /Fm1 Do Q
            q BT /F1 120 Tf 4 Tr 0 g 72 80 Td (CLIP) Tj ET 1 0.5 0 rg 0 0 612 792 re f Q
            """,
            "0 0.6 0 rg 60 600 300 100 re f 1 g BT /F1 24 Tf 0 Tr 80 640 Td (Text in a form) Tj ET");

    [Test]
    public async Task BackgroundSnapshot()
    {
        using var document = PdfiumDocument.Load(BackgroundPdf());
        byte[]? background = null;
        var html = document.RenderPageHtml(
            0,
            new()
            {
                Text = HtmlTextMode.Visible,
                ImageUrl = (_, png) =>
                {
                    background = png;
                    return "background.png";
                }
            });
        // The HTML, the page image it draws its text over, and PDFium's full render beside it.
        // The fill that "CLIP" clips is painted after the text, so in the render it hides the
        // text, but the HTML draws the text on top: text covered by later drawing is not yet
        // detected.
        await Verify(
            [
                new("html", html),
                new Target("png", new MemoryStream(background!), "background"),
                new Target("png", new MemoryStream(document.RenderPage(0)), "render")
            ])
            .Snapshot(
                """
                <div class="pdf-page" id="page-1" style="position:relative;overflow:hidden;width:612pt;max-width:100%;aspect-ratio:612/792;background:#ffffff;page:pdf-612x792">
                <img src="background.png" alt="" draggable="false" style="position:absolute;left:0;top:0;width:100%;height:100%;user-select:none">
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 612 792" preserveAspectRatio="none" xml:space="preserve" style="position:absolute;left:0;top:0;width:100%;height:100%;white-space:pre" fill="#000">
                <text x="72" y="102" font-size="28" textLength="245.87" lengthAdjust="spacingAndGlyphs" font-family="'Helvetica', sans-serif">Text over a drawing</text>
                <text x="72" y="272" font-size="36" textLength="202.1" lengthAdjust="spacingAndGlyphs" font-family="'Helvetica', sans-serif" fill="none" stroke="#0000ff" stroke-width="1">Outlined text</text>
                <text x="80" y="452" font-size="24" textLength="146.71" lengthAdjust="spacingAndGlyphs" font-family="'Helvetica', sans-serif" fill="#ffffff">Text in a form</text>
                <text x="72" y="712" font-size="120" textLength="266.76" lengthAdjust="spacingAndGlyphs" font-family="'Helvetica', sans-serif">CLIP</text>
                </svg>
                </div>

                """);
    }

    [Test]
    public async Task DocumentWithBackgrounds()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.multi_page_pdf);
        var html = document.RenderHtml(
            new()
            {
                Text = HtmlTextMode.Visible,
                ImageUrl = (index, _) => $"page{index + 1}.png"
            });
        await Verify(html, "html");
    }

    [Test]
    public async Task BackgroundRestoresTheText()
    {
        var pdf = SinglePage("1 0 0 rg 72 500 200 100 re f 0 g BT /F1 24 Tf 72 700 Td (Some text) Tj ET BT /F1 24 Tf 6 Tr 72 400 Td (Clipping) Tj ET");
        using var document = PdfiumDocument.Load(pdf);
        using var page = document.LoadPage(0);
        var render = page.Render();
        var saved = WithoutTrailer(document.Save());
        var text = page.GetText();

        page.RenderHtml(
            new()
            {
                Text = HtmlTextMode.Visible,
                ImageUrl = (_, _) => "page.png"
            });

        // The same page, the document and a save all show the text as before.
        await Assert.That(page.Render()).IsEquivalentTo(render);
        await Assert.That(document.RenderPage(0)).IsEquivalentTo(render);
        await Assert.That(WithoutTrailer(document.Save())).IsEquivalentTo(saved);
        await Assert.That(page.GetText()).IsEqualTo(text);
    }

    // Switching a text object's render mode marks it changed in PDFium, even when it is
    // switched back, and the next content regeneration rewrites the page's content streams in
    // full. So an edit after visible text HTML must save exactly as the same edit without it.
    [Test]
    public async Task BackgroundLeavesNoTraceForLaterEdits()
    {
        static byte[] EditAndSave(bool renderHtml)
        {
            using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
            using (var page = document.LoadPage(0))
            {
                if (renderHtml)
                {
                    page.RenderHtml(
                        new()
                        {
                            Text = HtmlTextMode.Visible,
                            ImageUrl = (_, _) => "page.png"
                        });
                }

                page.AddRectangle(new(72, 72, 144, 144), new(255, 0, 0, 255));
            }

            return WithoutTrailer(document.Save());
        }

        await Assert.That(EditAndSave(true)).IsEquivalentTo(EditAndSave(false));
    }

    // The document's own conversions hide the text in place, on a page they load and dispose
    // themselves, so the same holds after them for a page loaded afterwards.
    [Test]
    public async Task DocumentConversionLeavesNoTraceForLaterEdits()
    {
        static byte[] EditAndSave(bool renderHtml)
        {
            using var document = PdfiumDocument.Load(ProjectFiles.multi_page_pdf);
            if (renderHtml)
            {
                var options = new HtmlOptions
                {
                    Text = HtmlTextMode.Visible,
                    ImageUrl = (_, _) => "page.png"
                };
                document.RenderPageHtml(0, options);
                document.RenderHtml(TextWriter.Null, options);
            }

            using (var page = document.LoadPage(0))
            {
                page.AddRectangle(new(72, 72, 144, 144), new(255, 0, 0, 255));
            }

            return WithoutTrailer(document.Save());
        }

        await Assert.That(EditAndSave(true)).IsEquivalentTo(EditAndSave(false));
    }

    // Every save writes a fresh random file identifier into the trailer, so saves are compared
    // up to it.
    static byte[] WithoutTrailer(byte[] pdf)
    {
        var text = Encoding.Latin1.GetString(pdf);
        return pdf[..text.LastIndexOf("trailer", StringComparison.Ordinal)];
    }

    [Test]
    public async Task LazyLoadsLaterPages()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.multi_page_pdf);
        var options = new HtmlOptions
        {
            ImageUrl = (index, _) => $"{index + 1}.png"
        };
        var html = document.RenderHtml(options);
        // The first page is on screen at once, so it loads straight away.
        await Assert.That(html).Contains("<img src=\"1.png\" alt=\"\" draggable");
        await Assert.That(html).Contains("<img src=\"2.png\" alt=\"\" loading=\"lazy\" decoding=\"async\" draggable");
        await Assert.That(html).Contains("<img src=\"4.png\" alt=\"\" loading=\"lazy\" decoding=\"async\" draggable");

        var eager = document.RenderHtml(
            options with
            {
                LazyLoadImages = false
            });
        await Assert.That(eager).DoesNotContain("loading=");
    }

    // Records, as each page is written, how many pages have been rendered but not yet written.
    class PageRecorder(StrongBox<int> rendered) : StringWriter
    {
        public List<int> Order { get; } = [];
        public int MostWaiting { get; private set; }

        public override void Write(string? value)
        {
            if (value is not null && value.StartsWith("<div class=\"pdf-page\" id=\"page-"))
            {
                var start = "<div class=\"pdf-page\" id=\"page-".Length;
                Order.Add(int.Parse(value[start..value.IndexOf('"', start)]));
                MostWaiting = Math.Max(MostWaiting, Volatile.Read(ref rendered.Value) - Order.Count);
            }

            base.Write(value);
        }
    }

    static PdfiumDocument BlankDocument(int pages)
    {
        var document = PdfiumDocument.CreateNew();
        for (var index = 0; index < pages; index++)
        {
            document.NewPage(index, 200, 100).Dispose();
        }

        return document;
    }

    [Test]
    public async Task WritesPagesInOrderAsTheyAreRendered()
    {
        // Comfortably more pages than the workers may get ahead by.
        var count = PdfiumDocument.PageWindow * 3 + 5;
        using var document = BlankDocument(count);
        var rendered = new StrongBox<int>();
        var recorder = new PageRecorder(rendered);
        document.RenderHtml(
            recorder,
            new()
            {
                ImageUrl = (index, _) =>
                {
                    Interlocked.Increment(ref rendered.Value);
                    return $"{index + 1}.png";
                }
            });
        await Assert.That(recorder.Order.SequenceEqual(Enumerable.Range(1, count))).IsTrue();
        // Never more than the window rendered and waiting to be written: the document is not
        // held whole.
        await Assert.That(recorder.MostWaiting).IsLessThanOrEqualTo(PdfiumDocument.PageWindow);
        await Assert.That(recorder.ToString()).IsEqualTo(
            document.RenderHtml(
                new()
                {
                    ImageUrl = (index, _) => $"{index + 1}.png"
                }));
    }

    [Test]
    public async Task PageFailureStopsTheRender()
    {
        using var document = BlankDocument(PdfiumDocument.PageWindow * 3);
        var options = new HtmlOptions
        {
            ImageUrl = (index, _) =>
            {
                if (index == 5)
                {
                    throw new InvalidOperationException("page 6 failed");
                }

                return "page.png";
            }
        };
        await Assert.That(() => document.RenderHtml(TextWriter.Null, options))
            .Throws<InvalidOperationException>()
            .WithMessage("page 6 failed");
    }

    // Fails on the first page it is given, and counts what it is given after that.
    class FailingWriter : StringWriter
    {
        public bool Failed { get; private set; }
        public int WritesAfterFailing { get; private set; }

        public override void Write(string? value)
        {
            if (Failed)
            {
                WritesAfterFailing++;
                return;
            }

            if (value is not null && value.StartsWith("<div class=\"pdf-page\""))
            {
                Failed = true;
                throw new IOException("disk full");
            }

            base.Write(value);
        }
    }

    [Test]
    public async Task WriterFailureStopsTheRender()
    {
        // Enough pages that others are still being rendered when the writer fails.
        using var document = BlankDocument(PdfiumDocument.PageWindow * 3);
        var writer = new FailingWriter();
        await Assert.That(() => document.RenderHtml(writer, textOnly))
            .Throws<IOException>()
            .WithMessage("disk full");
        // Once the writer has failed, no later page is written to it.
        await Assert.That(writer.WritesAfterFailing).IsEqualTo(0);
    }

    [Test]
    public async Task ImageOnly()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        var html = document.RenderPageHtml(
            0,
            new()
            {
                Text = HtmlTextMode.None,
                ImageUrl = (_, _) => "page.png"
            });
        await Assert.That(html).DoesNotContain("<svg");
        // With no text, the image describes the page.
        await Assert.That(html).Contains("alt=\"Page 1\"");
    }

    [Test]
    public async Task PageIdPrefix()
    {
        using var document = PdfiumDocument.Load(PdfWithLinks());
        var html = document.RenderPageHtml(
            0,
            textOnly with
            {
                PageIdPrefix = "doc1-"
            });
        await Assert.That(html).Contains("id=\"doc1-1\"");
        await Assert.That(html).Contains("href=\"#doc1-2\"");
    }

    // Two pages. The first links to the second in both ways a PDF can, and to a URL, another
    // file, an embedded file, an application and a script, of which HTML should only get the
    // first four. Its text also holds a URL with no link annotation over it.
    internal static byte[] PdfWithLinks() =>
        CraftedPdf.Build(new()
        {
            [1] = "<< /Type /Catalog /Pages 2 0 R /Outlines 20 0 R /Lang (en-GB) >>",
            [2] = "<< /Type /Pages /Kids [3 0 R 6 0 R] /Count 2 >>",
            [3] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> /Annots [10 0 R 11 0 R 12 0 R 13 0 R 14 0 R 15 0 R 16 0 R] >>",
            [4] = CraftedPdf.Stream("BT /F1 12 Tf 72 700 Td (See https://example.com/text for more) Tj ET"),
            [5] = CraftedPdf.Helvetica,
            [6] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 7 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            [7] = CraftedPdf.Stream("BT /F1 12 Tf 72 700 Td (Second page) Tj ET"),
            [10] = "<< /Type /Annot /Subtype /Link /Rect [72 600 300 620] /Dest [6 0 R /XYZ 0 792 0] >>",
            [11] = "<< /Type /Annot /Subtype /Link /Rect [72 570 300 590] /A << /S /GoTo /D [6 0 R /Fit] >> >>",
            [12] = "<< /Type /Annot /Subtype /Link /Rect [72 540 300 560] /A << /S /URI /URI (https://example.com/a?b=c&d=\"e\") >> >>",
            [13] = "<< /Type /Annot /Subtype /Link /Rect [72 510 300 530] /A << /S /GoToR /F (remote.pdf) /D [0 /Fit] >> >>",
            [14] = "<< /Type /Annot /Subtype /Link /Rect [72 480 300 500] /A << /S /GoToE /T << /R /C /N (embedded.pdf) >> /D [0 /Fit] >> >>",
            [15] = "<< /Type /Annot /Subtype /Link /Rect [72 450 300 470] /A << /S /Launch /F (tool.exe) >> >>",
            [16] = "<< /Type /Annot /Subtype /Link /Rect [72 420 300 440] /A << /S /URI /URI (javascript:alert\\(1\\)) >> >>",
            [20] = "<< /Type /Outlines /First 21 0 R /Last 22 0 R /Count 2 >>",
            [21] = "<< /Title (Start <here>) /Parent 20 0 R /Next 22 0 R /Dest [3 0 R /Fit] /First 23 0 R /Last 23 0 R /Count 1 >>",
            [22] = "<< /Title (Launch) /Parent 20 0 R /Prev 21 0 R /A << /S /Launch /F (tool.exe) >> >>",
            [23] = "<< /Title (Second) /Parent 21 0 R /Dest [6 0 R /Fit] >>"
        });

    [Test]
    public async Task Links()
    {
        using var document = PdfiumDocument.Load(PdfWithLinks());
        var html = document.RenderPageHtml(0, textOnly);
        // Both links to the second page, the URL, the other file and the URL found in the text.
        await Verify(html, "html")
            .Snapshot(
                """
                <div class="pdf-page" id="page-1" style="position:relative;overflow:hidden;width:612pt;max-width:100%;aspect-ratio:612/792;background:#ffffff;page:pdf-612x792">
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 612 792" preserveAspectRatio="none" xml:space="preserve" style="position:absolute;left:0;top:0;width:100%;height:100%;white-space:pre" fill="#000">
                <text x="72" y="92" font-size="12" textLength="202.75" lengthAdjust="spacingAndGlyphs" font-family="'Helvetica', sans-serif">See https://example.com/text for more</text>
                <a href="#page-2"><rect x="72" y="172" width="228" height="20" fill="transparent"/></a>
                <a href="#page-2"><rect x="72" y="202" width="228" height="20" fill="transparent"/></a>
                <a href="https://example.com/a?b=c&amp;d=&quot;e&quot;"><rect x="72" y="232" width="228" height="20" fill="transparent"/></a>
                <a href="remote.pdf"><rect x="72" y="262" width="228" height="20" fill="transparent"/></a>
                <a href="https://example.com/text"><rect x="97.48" y="83.26" width="129.18" height="11.11" fill="transparent"/></a>
                </svg>
                </div>

                """);
        await Assert.That(html).DoesNotContain("javascript");
        await Assert.That(html).DoesNotContain("tool.exe");
        await Assert.That(html).DoesNotContain("embedded.pdf");
    }

    [Test]
    public async Task NoLinks()
    {
        using var document = PdfiumDocument.Load(PdfWithLinks());
        var html = document.RenderPageHtml(
            0,
            textOnly with
            {
                IncludeLinks = false
            });
        await Assert.That(html).DoesNotContain("<a ");
    }

    [Test]
    public async Task OutlineAndLanguage()
    {
        using var document = PdfiumDocument.Load(PdfWithLinks());
        await Assert.That(document.GetLanguage()).IsEqualTo("en-GB");
        var html = document.RenderHtml(textOnly);
        await Verify(html, "html");

        var withoutOutline = document.RenderHtml(
            textOnly with
            {
                IncludeOutline = false
            });
        await Assert.That(withoutOutline).DoesNotContain("<nav");
    }

    [Test]
    public async Task NoLanguage()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        await Assert.That(document.GetLanguage()).IsNull();
    }

    [Test]
    public async Task Title()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        var html = document.RenderHtml(
            textOnly with
            {
                Title = "A <b>& \"c\""
            });
        await Assert.That(html).Contains("<title>A &lt;b&gt;&amp; &quot;c&quot;</title>");
    }

    // A page turned a quarter turn, with a crop box away from the origin, holding a line of
    // text and a link.
    internal static byte[] RotatedPdf() =>
        CraftedPdf.Build(new()
        {
            [1] = "<< /Type /Catalog /Pages 2 0 R >>",
            [2] = "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            [3] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /CropBox [100 100 512 692] /Rotate 90 /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> /Annots [6 0 R] >>",
            [4] = CraftedPdf.Stream("BT /F1 20 Tf 150 600 Td (Rotated) Tj ET"),
            [5] = CraftedPdf.Helvetica,
            [6] = "<< /Type /Annot /Subtype /Link /Rect [150 590 300 620] /A << /S /URI /URI (https://example.com/) >> >>"
        });

    [Test]
    public async Task RotatedPage()
    {
        using var document = PdfiumDocument.Load(RotatedPdf());
        // Turned clockwise, the crop box (412 by 592) is shown 592 wide and 412 high, its
        // bottom edge on the left and its left edge along the top. So the text origin
        // (150, 600), 500 above the crop box bottom and 50 right of its left edge, is shown
        // 500 across and 50 down, and the text runs down the page.
        await Verify(document.RenderPageHtml(0, textOnly), "html")
            .Snapshot(
                """
                <div class="pdf-page" id="page-1" style="position:relative;overflow:hidden;width:592pt;max-width:100%;aspect-ratio:592/412;background:#ffffff;page:pdf-592x412">
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 592 412" preserveAspectRatio="none" xml:space="preserve" style="position:absolute;left:0;top:0;width:100%;height:100%;white-space:pre" fill="#000">
                <text x="500" y="50" font-size="20" textLength="70.04" lengthAdjust="spacingAndGlyphs" transform="rotate(90 500 50)" font-family="'Helvetica', sans-serif">Rotated</text>
                <a href="https://example.com/"><rect x="490" y="50" width="30" height="150" fill="transparent"/></a>
                </svg>
                </div>

                """);
    }

    // A malformed page whose crop box lies outside its media box, so the two do not overlap.
    // It still renders to PNG, so it must render to HTML too, rather than failing the document.
    [Test]
    public async Task CropBoxOutsideMediaBox()
    {
        var pdf = CraftedPdf.Build(new()
        {
            [1] = "<< /Type /Catalog /Pages 2 0 R >>",
            [2] = "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            [3] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /CropBox [700 900 800 1000] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            [4] = CraftedPdf.Stream("BT /F1 12 Tf 72 700 Td (Text) Tj ET"),
            [5] = CraftedPdf.Helvetica
        });
        using var document = PdfiumDocument.Load(pdf);
        await Assert.That(document.RenderPage(0).Length).IsGreaterThan(0);
        var html = document.RenderHtml(
            new()
            {
                ImageUrl = (_, _) => "page.png"
            });
        await Assert.That(html).Contains("<img src=\"page.png\"");
    }

    [Test]
    public async Task RotatedText()
    {
        var pdf = CraftedPdf.Build(new()
        {
            [1] = "<< /Type /Catalog /Pages 2 0 R >>",
            [2] = "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            [3] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            // Running up the page, a quarter turn anticlockwise.
            [4] = CraftedPdf.Stream("BT /F1 20 Tf 0 1 -1 0 300 100 Tm (Upwards) Tj ET"),
            [5] = CraftedPdf.Helvetica
        });
        using var document = PdfiumDocument.Load(pdf);
        var html = document.RenderPageHtml(0, textOnly);
        await Assert.That(html).Contains("<text x=\"300\" y=\"692\"");
        await Assert.That(html).Contains("transform=\"rotate(-90 300 692)\"");
    }

    [Test]
    public async Task VisibleStyles()
    {
        var pdf = CraftedPdf.Build(new()
        {
            [1] = "<< /Type /Catalog /Pages 2 0 R >>",
            [2] = "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            [3] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R /F2 6 0 R /F3 7 0 R >> >> >>",
            [4] = CraftedPdf.Stream(
                """
                BT /F1 12 Tf 1 0 0 rg 72 700 Td (Red bold serif) Tj ET
                BT /F2 12 Tf 0 0 1 rg 72 680 Td (Blue italic sans) Tj ET
                BT /F3 12 Tf 0 g 72 660 Td (Fixed) Tj ET
                BT /F3 12 Tf 3 Tr 72 640 Td (Hidden) Tj ET
                """),
            [5] = "<< /Type /Font /Subtype /Type1 /BaseFont /Times-Bold /Encoding /WinAnsiEncoding >>",
            [6] = "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Oblique /Encoding /WinAnsiEncoding >>",
            [7] = "<< /Type /Font /Subtype /Type1 /BaseFont /Courier /Encoding /WinAnsiEncoding >>"
        });
        using var document = PdfiumDocument.Load(pdf);
        // Text drawn invisibly is left out of visible text.
        await Verify(document.RenderPageHtml(0, textOnly), "html")
            .Snapshot(
                """
                <div class="pdf-page" id="page-1" style="position:relative;overflow:hidden;width:612pt;max-width:100%;aspect-ratio:612/792;background:#ffffff;page:pdf-612x792">
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 612 792" preserveAspectRatio="none" xml:space="preserve" style="position:absolute;left:0;top:0;width:100%;height:100%;white-space:pre" fill="#000">
                <text x="72" y="92" font-size="12" textLength="72.89" lengthAdjust="spacingAndGlyphs" font-family="'Times', serif" font-weight="700" fill="#ff0000">Red bold serif</text>
                <text x="72" y="112" font-size="12" textLength="80.04" lengthAdjust="spacingAndGlyphs" font-family="'Helvetica', sans-serif" font-style="italic" fill="#0000ff">Blue italic sans</text>
                <text x="72" y="132" font-size="12" textLength="36" lengthAdjust="spacingAndGlyphs" font-family="'Courier', monospace">Fixed</text>
                </svg>
                </div>

                """);
        // But kept in the overlay, where it makes a scan searchable.
        var overlay = document.RenderPageHtml(
            0,
            new()
            {
                ImageUrl = (_, _) => "page.png"
            });
        await Assert.That(overlay).Contains(">Hidden</text>");
        await Assert.That(overlay).DoesNotContain("fill=\"#ff0000\"");
    }

    // Text set at size 1 and scaled up by its text matrix, as cairo writes it, and text whose
    // size is doubled by the page's transformation.
    internal static byte[] ScaledTextPdf() =>
        CraftedPdf.Build(new()
        {
            [1] = "<< /Type /Catalog /Pages 2 0 R >>",
            [2] = "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            [3] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            [4] = CraftedPdf.Stream(
                """
                BT /F1 1 Tf 24 0 0 24 72 700 Tm (Scaled by the matrix) Tj ET
                q 2 0 0 2 0 0 cm BT /F1 10 Tf 36 300 Td (Doubled by the page) Tj ET Q
                """),
            [5] = CraftedPdf.Helvetica
        });

    [Test]
    public async Task FontSizeIncludesTheTransformation()
    {
        using var document = PdfiumDocument.Load(ScaledTextPdf());
        // PDFium reports both as set at their Tf size, 1 and 10; drawn, they are 24 and 20.
        await Verify(document.RenderPageHtml(0, textOnly), "html")
            .Snapshot(
                """
                <div class="pdf-page" id="page-1" style="position:relative;overflow:hidden;width:612pt;max-width:100%;aspect-ratio:612/792;background:#ffffff;page:pdf-612x792">
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 612 792" preserveAspectRatio="none" xml:space="preserve" style="position:absolute;left:0;top:0;width:100%;height:100%;white-space:pre" fill="#000">
                <text x="72" y="92" font-size="24" textLength="217.42" lengthAdjust="spacingAndGlyphs" font-family="'Helvetica', sans-serif">Scaled by the matrix</text>
                <text x="72" y="192" font-size="20" textLength="184.56" lengthAdjust="spacingAndGlyphs" font-family="'Helvetica', sans-serif">Doubled by the page</text>
                </svg>
                </div>

                """);
    }

    // Text drawn as an outline, filled and outlined, and only added to the clipping path.
    internal static byte[] StrokedTextPdf() =>
        CraftedPdf.Build(new()
        {
            [1] = "<< /Type /Catalog /Pages 2 0 R >>",
            [2] = "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            [3] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            [4] = CraftedPdf.Stream(
                """
                BT /F1 36 Tf 1 Tr 0 0 1 RG 2 w 72 700 Td (Outline) Tj ET
                BT /F1 36 Tf 2 Tr 1 0 0 rg 0 0 1 RG 1 w 72 640 Td (Filled and outlined) Tj ET
                BT /F1 36 Tf 7 Tr 72 580 Td (Clip only) Tj ET
                BT /F1 36 Tf 0 Tr 0 g 72 520 Td (Filled) Tj ET
                """),
            [5] = CraftedPdf.Helvetica
        });

    [Test]
    public async Task StrokedText()
    {
        using var document = PdfiumDocument.Load(StrokedTextPdf());
        // Text that only clips draws nothing, so it is left out like invisible text.
        await Verify(document.RenderPageHtml(0, textOnly), "html")
            .Snapshot(
                """
                <div class="pdf-page" id="page-1" style="position:relative;overflow:hidden;width:612pt;max-width:100%;aspect-ratio:612/792;background:#ffffff;page:pdf-612x792">
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 612 792" preserveAspectRatio="none" xml:space="preserve" style="position:absolute;left:0;top:0;width:100%;height:100%;white-space:pre" fill="#000">
                <text x="72" y="92" font-size="36" textLength="114.05" lengthAdjust="spacingAndGlyphs" font-family="'Helvetica', sans-serif" fill="none" stroke="#0000ff" stroke-width="2">Outline</text>
                <text x="72" y="152" font-size="36" textLength="292.14" lengthAdjust="spacingAndGlyphs" font-family="'Helvetica', sans-serif" fill="#ff0000" stroke="#0000ff" stroke-width="1">Filled and outlined</text>
                <text x="72" y="272" font-size="36" textLength="86" lengthAdjust="spacingAndGlyphs" font-family="'Helvetica', sans-serif">Filled</text>
                </svg>
                </div>

                """);
    }

    [Test]
    public async Task ShearedText()
    {
        // Slanted by its text matrix along a horizontal baseline, and slanted and turned 30
        // degrees anticlockwise. PDFium measures its character angle from the slanted y axis,
        // so it reports the first as turned too; the matrix's x axis is the true baseline.
        var pdf = CraftedPdf.Build(new()
        {
            [1] = "<< /Type /Catalog /Pages 2 0 R >>",
            [2] = "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            [3] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            [4] = CraftedPdf.Stream(
                """
                BT /F1 1 Tf 20 0 10 20 72 600 Tm (Sheared along the line) Tj ET
                BT /F1 1 Tf 17.3205 10 -1.3397 22.3205 72 300 Tm (Sheared and turned) Tj ET
                """),
            [5] = CraftedPdf.Helvetica
        });
        using var document = PdfiumDocument.Load(pdf);
        var html = document.RenderPageHtml(0, textOnly);
        await Assert.That(html).Contains("<text x=\"72\" y=\"192\" font-size=\"20\"");
        await Assert.That(html).Contains("transform=\"rotate(-30 72 492)\"");
        await Assert.That(html.Split("<text ").Length - 1).IsEqualTo(2);
    }

    [Test]
    public async Task LigatureStaysInOneRun()
    {
        // The font's ToUnicode map reads the glyph for "A" as the three letters "ffi", as a
        // ligature glyph would be, so PDFium reports f, f and i at one origin.
        const string toUnicode =
            """
            /CIDInit /ProcSet findresource begin 12 dict begin begincmap
            /CMapName /Ligature def /CMapType 2 def
            1 begincodespacerange <00> <FF> endcodespacerange
            2 beginbfrange <20> <40> <0020> <42> <7E> <0042> endbfrange
            1 beginbfchar <41> <006600660069> endbfchar
            endcmap CMapName currentdict /CMap defineresource pop end end
            """;
        var pdf = CraftedPdf.Build(new()
        {
            [1] = "<< /Type /Catalog /Pages 2 0 R >>",
            [2] = "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            [3] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            [4] = CraftedPdf.Stream("BT /F1 24 Tf 72 700 Td (eAcient) Tj ET"),
            [5] = "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding /ToUnicode 6 0 R >>",
            [6] = CraftedPdf.Stream(toUnicode)
        });
        using var document = PdfiumDocument.Load(pdf);
        var html = document.RenderPageHtml(0, textOnly);
        await Assert.That(html).Contains(">efficient</text>");
        await Assert.That(html.Split("<text ").Length - 1).IsEqualTo(1);
    }

    [Test]
    public async Task SkippedSurrogatePairLeavesNoHalf()
    {
        // The ToUnicode map reads "A" as U+1F600, beyond the Basic Multilingual Plane, which
        // PDFium on Windows reports as two characters, the halves of a surrogate pair. Drawn
        // invisibly after visible text, visible HTML leaves the pair out, and must leave out
        // both halves rather than tack the second onto the visible text.
        const string toUnicode =
            """
            /CIDInit /ProcSet findresource begin 12 dict begin begincmap
            /CMapName /Emoji def /CMapType 2 def
            1 begincodespacerange <00> <FF> endcodespacerange
            2 beginbfrange <20> <40> <0020> <42> <7E> <0042> endbfrange
            1 beginbfchar <41> <D83DDE00> endbfchar
            endcmap CMapName currentdict /CMap defineresource pop end end
            """;
        var pdf = CraftedPdf.Build(new()
        {
            [1] = "<< /Type /Catalog /Pages 2 0 R >>",
            [2] = "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            [3] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            [4] = CraftedPdf.Stream("BT /F1 24 Tf 72 700 Td (Hello) Tj ET BT /F1 24 Tf 3 Tr 160 700 Td (A) Tj ET"),
            [5] = "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding /ToUnicode 6 0 R >>",
            [6] = CraftedPdf.Stream(toUnicode)
        });
        using var document = PdfiumDocument.Load(pdf);
        var visible = document.RenderPageHtml(0, textOnly);
        await Assert.That(visible).Contains(">Hello</text>");
        await Assert.That(visible).DoesNotContain("�");
        // The overlay keeps invisible text, so it has the whole pair.
        var overlay = document.RenderPageHtml(
            0,
            new()
            {
                ImageUrl = (_, _) => "page.png"
            });
        await Assert.That(overlay).Contains("\U0001F600");
    }

    [Test]
    public async Task ColumnsAreSeparateRuns()
    {
        var pdf = CraftedPdf.Build(new()
        {
            [1] = "<< /Type /Catalog /Pages 2 0 R >>",
            [2] = "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            [3] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            [4] = CraftedPdf.Stream("BT /F1 12 Tf 72 700 Td (Name) Tj 200 0 Td (Value) Tj ET"),
            [5] = CraftedPdf.Helvetica
        });
        using var document = PdfiumDocument.Load(pdf);
        var html = document.RenderPageHtml(0, textOnly);
        await Assert.That(html).Contains("<text x=\"72\" y=\"92\"");
        await Assert.That(html).Contains("<text x=\"272\" y=\"92\"");
    }

    [Test]
    public async Task ListIndentsAreSeparateRuns()
    {
        // A bullet and a list number, each followed by a space and then text at the indent,
        // over an em past them: Word's hanging indent. Then a bullet with a single word.
        var pdf = CraftedPdf.Build(new()
        {
            [1] = "<< /Type /Catalog /Pages 2 0 R >>",
            [2] = "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            [3] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            [4] = CraftedPdf.Stream(
                """
                BT /F1 11 Tf 90 700 Td (\225 ) Tj 18 0 Td (Donec ullamcorper fringilla eros) Tj ET
                BT /F1 11 Tf 90 680 Td (1. ) Tj 18 0 Td (Fusce iaculis est quis) Tj ET
                BT /F1 11 Tf 90 660 Td (\225 ) Tj 18 0 Td (Alone) Tj ET
                """),
            [5] = CraftedPdf.Helvetica
        });
        using var document = PdfiumDocument.Load(pdf);
        var html = document.RenderPageHtml(
            0,
            new()
            {
                IncludeImage = false
            });
        // Each line's text starts at the indent, rather than spread from the bullet.
        await Assert.That(html).Contains("<text x=\"108\" y=\"92\" font-size=\"11\" textLength=\"154.65\" lengthAdjust=\"spacingAndGlyphs\">Donec ullamcorper fringilla eros</text>");
        await Assert.That(html).Contains("<text x=\"108\" y=\"112\" font-size=\"11\" textLength=\"104.53\" lengthAdjust=\"spacingAndGlyphs\">Fusce iaculis est quis</text>");
        await Assert.That(html).Contains("<text x=\"108\" y=\"132\" font-size=\"11\" textLength=\"28.13\" lengthAdjust=\"spacingAndGlyphs\">Alone</text>");
        await Assert.That(html).Contains(">1.</text>");
        await Assert.That(html.Split(">•</text>").Length - 1).IsEqualTo(2);
        await Assert.That(html.Split("<text ").Length - 1).IsEqualTo(6);
    }

    [Test]
    public async Task JustifiedLinesAreOneRunEach()
    {
        // Lines spread to the margin by word spacing, as justified text is: the first with
        // spaces of about an em, the second a third of that, and a dot leader.
        var pdf = CraftedPdf.Build(new()
        {
            [1] = "<< /Type /Catalog /Pages 2 0 R >>",
            [2] = "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            [3] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            [4] = CraftedPdf.Stream(
                """
                BT /F1 11 Tf 8 Tw 72 700 Td (A line set wide by word spacing) Tj ET
                BT /F1 11 Tf 1 Tw 72 686 Td (A line set a little wide by word spacing) Tj ET
                BT /F1 11 Tf 10 Tw 72 672 Td (. . . . . . . .) Tj ET
                """),
            [5] = CraftedPdf.Helvetica
        });
        using var document = PdfiumDocument.Load(pdf);
        var html = document.RenderPageHtml(
            0,
            new()
            {
                IncludeImage = false
            });
        await Assert.That(html).Contains(">A line set wide by word spacing</text>");
        await Assert.That(html).Contains(">A line set a little wide by word spacing</text>");
        await Assert.That(html).Contains(">. . . . . . . .</text>");
        await Assert.That(html.Split("<text ").Length - 1).IsEqualTo(3);
    }

    [Test]
    public async Task EscapesText()
    {
        using var document = PdfiumDocument.CreateNew();
        using (var page = document.NewPage(0, 612, 792))
        {
            page.AddText("a<b & \"c\" > d", 72, 700);
        }

        var html = document.RenderPageHtml(0, textOnly);
        await Assert.That(html).Contains(">a&lt;b &amp; &quot;c&quot; &gt; d</text>");
    }

    [Test]
    public async Task EmptyPage()
    {
        using var document = PdfiumDocument.CreateNew();
        document.NewPage(0, 200, 100).Dispose();
        var html = document.RenderPageHtml(0, textOnly);
        await Assert.That(html).IsEqualTo("<div class=\"pdf-page\" id=\"page-1\" style=\"position:relative;overflow:hidden;width:200pt;max-width:100%;aspect-ratio:200/100;background:#ffffff;page:pdf-200x100\">\n</div>\n");
    }

    [Test]
    public async Task Validates()
    {
        using var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        await Assert.That(() => document.RenderPageHtml(0, new() { Render = null! })).Throws<ArgumentException>();
        await Assert.That(() => document.RenderPageHtml(0, new() { PageIdPrefix = null! })).Throws<ArgumentException>();
        await Assert.That(() => document.RenderPageHtml(0, new() { Text = (HtmlTextMode) 9 })).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => document.RenderPageHtml(0, new() { Render = new() { Dpi = 0 } })).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => document.RenderPageHtml(1)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => document.RenderHtml((TextWriter) null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Disposed()
    {
        var document = PdfiumDocument.Load(ProjectFiles.sample_pdf);
        var page = document.LoadPage(0);
        page.Dispose();
        await Assert.That(() => page.RenderHtml()).Throws<ObjectDisposedException>();
        document.Dispose();
        await Assert.That(() => document.RenderHtml()).Throws<ObjectDisposedException>();
    }
}
