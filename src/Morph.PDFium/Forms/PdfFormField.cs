namespace Morph.PDFium;

/// <summary>A single interactive form field (widget) on a page.</summary>
/// <param name="Name">The fully qualified field name.</param>
/// <param name="Type">The field type.</param>
/// <param name="Value">The field's current value, when it has one.</param>
/// <param name="Rectangle">The widget's bounding rectangle in page points.</param>
public readonly record struct PdfFormField(string Name, FormFieldType Type, string? Value, PdfRectangle Rectangle);
