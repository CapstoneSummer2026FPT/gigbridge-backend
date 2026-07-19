using Application.Features.Premium.Client.SmartTalentMatching.GetMatches.DTOs;
using MediatR;

namespace Application.Features.Premium.Client.SmartTalentMatching.GetMatches.Queries;

public sealed record GetTalentMatchesQuery(Guid UserId, Guid JobPostId, int TopK = 10)
    : IRequest<TalentMatchingResultDto>;
