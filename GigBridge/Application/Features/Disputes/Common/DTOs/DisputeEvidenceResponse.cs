using Domain.Enums;

namespace Application.Features.Disputes.Common.DTOs;

public sealed record DisputeEvidenceResponse(
    Guid DisputeEvidenceId,
    Guid DisputesId,
    Guid UploadedById,
    string FileName,
    string FileUrl,
    long? FileSize,
    string? Description,
    DateTime CreatedAt);
