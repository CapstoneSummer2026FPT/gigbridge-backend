namespace Application.Features.Contracts.ProductHandoffs.Common.DTOs;

public sealed record ContractProductHandoffResponse(
    Guid ContractProductHandoffId,
    Guid ContractId,
    Guid SubmittedByUserId,
    int SourceType,
    string? FileName,
    string? FileUrl,
    string? MimeType,
    long? FileSizeBytes,
    string? ExternalUrl,
    string? Note,
    int Version,
    bool IsCurrent,
    Guid? ReceivedByUserId,
    DateTime? ReceivedAt,
    DateTime CreatedAt);

public sealed record ProductHandoffDownloadResponse(
    Guid ContractProductHandoffId,
    Guid ContractId,
    int SourceType,
    string Url,
    string? FileName);
