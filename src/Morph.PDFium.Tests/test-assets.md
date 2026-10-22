# Test assets

| File | Pages | What it exercises | Source |
|------|-------|-------------------|--------|
| `sample.pdf` | 1 | A paragraph of text in an embedded font subset | Made with [Morph](https://github.com/Papyrine/Morph)'s PDF exporter |
| `multi-page.pdf` | 4 | Paragraphs of text across pages | Made with Morph's PDF exporter |
| `irs-f1040.pdf` | 2 | A dense form: ruled tables, checkboxes, about 200 form fields (a hybrid XFA form, so AcroForm fields too), many font sizes and weights | IRS Form 1040 (2025), <https://www.irs.gov/pub/irs-pdf/f1040.pdf>, downloaded 2026-10-22 |
| `nasa-training-for-space.pdf` | 2 | A photo with a caption, text wrapped around it, coloured headings, the NASA insignia, vertical text, two columns | NASA fact sheet "Training for space" (Johnson Space Center, 2006), <https://www.nasa.gov/wp-content/uploads/2015/10/160410main_space_training_fact_sheet.pdf>, downloaded 2026-10-22 |

The IRS and NASA documents are works of the United States government, which are not subject
to copyright in the United States ([17 U.S.C. § 105](https://www.law.cornell.edu/uscode/text/17/105)).
They are kept here unchanged, only as rendering test input. The NASA insignia in the fact
sheet is subject to NASA's usage guidelines on endorsement
([14 CFR Part 1221](https://www.ecfr.gov/current/title-14/chapter-V/part-1221)), which
test input does not engage.

Do not commit third-party PDFs whose licence is unclear, such as pdf2htmlEX's test files
(GPLv3, many sourced from bug reports). Run them through `MORPH_PDFIUM_HTML_CORPUS` locally
instead.
