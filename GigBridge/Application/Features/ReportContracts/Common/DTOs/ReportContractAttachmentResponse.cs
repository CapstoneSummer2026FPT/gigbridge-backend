namespace Application.Features.ReportContracts.Common.DTOs;

public sealed record ReportContractAttachmentResponse(
    Guid ReportContractAttachmentId,
    string FileUrl,
    string FileName,
    string ContentType,
    long FileSize,
    DateTime UploadedAt);
