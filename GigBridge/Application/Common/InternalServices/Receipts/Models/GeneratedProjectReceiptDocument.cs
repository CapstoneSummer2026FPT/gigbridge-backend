namespace Application.Common.InternalServices.Receipts.Models;

public sealed record GeneratedProjectReceiptDocument(
    byte[] Content,
    string FileName,
    string MimeType);
