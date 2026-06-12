namespace Morph.PDFium;

/// <summary>Raised when PDFium rejects a document or fails a rendering call.</summary>
public class PdfiumException(string message) :
    Exception(message);
