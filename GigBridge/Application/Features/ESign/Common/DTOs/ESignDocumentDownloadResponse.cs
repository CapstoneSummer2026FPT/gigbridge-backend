namespace Application.Features.ESign.Common.DTOs;

public sealed record ESignDocumentDownloadResponse(
    byte[] Content,
    string FileName,
    string ContentType);
