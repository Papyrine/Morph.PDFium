namespace Morph.PDFium;

/// <summary>
/// A digital signature in the document. Reading the signature exposes its metadata and the
/// raw signed contents; verifying the cryptographic signature is left to the caller (PDFium
/// does not validate signatures).
/// </summary>
/// <param name="Reason">The human-readable reason for signing, when present.</param>
/// <param name="SubFilter">The signature encoding (e.g. "adbe.pkcs7.detached"), when present.</param>
/// <param name="SigningTime">The raw signing time (PDF date string), when present in the dictionary.</param>
/// <param name="Contents">The signature value — a DER-encoded PKCS#1 or PKCS#7 blob.</param>
/// <param name="ByteRange">Pairs of (offset, length) describing the byte ranges the signature covers.</param>
/// <param name="DocMdpPermission">The DocMDP permission level (1-3), or null when not a certifying signature.</param>
public sealed record PdfSignature(
    string? Reason,
    string? SubFilter,
    string? SigningTime,
    byte[] Contents,
    int[] ByteRange,
    uint? DocMdpPermission);
