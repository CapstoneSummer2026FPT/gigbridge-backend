namespace Application.Features.Disputes.Common.DTOs;

public sealed record DisputeEvidenceDownloadResponse(
    Guid DisputeEvidenceId,
    string FileName,
    string DownloadUrl);
