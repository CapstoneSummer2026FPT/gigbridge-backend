using Application.Features.Receipts.Common.DTOs;

namespace Application.Common.Interfaces.IService;

public interface IProjectReceiptDocumentGenerator
{
    GeneratedProjectReceiptDocument Generate(ProjectReceiptSnapshot snapshot, string documentHashSha256);
}

public sealed record GeneratedProjectReceiptDocument(byte[] Content, string FileName, string MimeType);
