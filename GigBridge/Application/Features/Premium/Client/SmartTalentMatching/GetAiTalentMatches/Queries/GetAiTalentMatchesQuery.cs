using System;
using Application.Features.Premium.Client.SmartTalentMatching.GetAiTalentMatches.DTOs;
using MediatR;

namespace Application.Features.Premium.Client.SmartTalentMatching.GetAiTalentMatches.Queries;

public sealed record GetAiTalentMatchesQuery(Guid UserId, Guid JobPostId, int TopK = 10,
    AiTalentMatchingFiltersDto? Filters = null)
    : IRequest<AiTalentMatchingResultDto>;
