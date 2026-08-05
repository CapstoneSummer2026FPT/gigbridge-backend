using Application.Features.Elo.Common;
using Application.Features.Elo.DTOs;
using MediatR;

namespace Application.Features.Elo.Commands.UploadEloAppealEvidence;

/// <summary>
/// Adds more evidence to a user's own appeal while it is still Pending.
/// </summary>
public sealed record UploadEloAppealEvidenceCommand(
    Guid UserId,
    Guid AppealId,
    IReadOnlyList<EloAppealFile> Files) : IRequest<IReadOnlyList<EloAppealEvidenceDto>>;
