namespace Application.Features.ESign.Common.DTOs;

public sealed record ESignPdfArtifactResponse(
    Guid DocumentId,
    string FileName);
