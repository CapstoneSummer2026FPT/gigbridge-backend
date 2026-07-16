namespace Application.Features.Disputes.Common.DTOs;

public sealed record DisputeEvidenceResponse(
    Guid DisputeEvidenceId,
    Guid UploadedById,
    string FileName,
    long? FileSize,
    string? Description,
    DateTime CreatedAt);
