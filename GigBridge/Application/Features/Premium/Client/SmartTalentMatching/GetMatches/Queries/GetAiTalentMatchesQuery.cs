using Application.Features.Premium.Client.SmartTalentMatching.GetMatches.DTOs;
using MediatR;

namespace Application.Features.Premium.Client.SmartTalentMatching.GetMatches.Queries;

public sealed record GetAiTalentMatchesQuery(Guid UserId, Guid JobPostId, int TopK = 10,
    AiTalentMatchingFiltersDto? Filters = null)
    : IRequest<AiTalentMatchingResultDto>;
