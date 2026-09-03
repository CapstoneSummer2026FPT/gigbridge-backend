using System;
using Application.Features.Premium.Client.SmartTalentMatching.GetTalentMatches.DTOs;
using MediatR;

namespace Application.Features.Premium.Client.SmartTalentMatching.GetTalentMatches.Queries;

public sealed record GetTalentMatchesQuery(
    Guid UserId,
    Guid JobPostId,
    int TopK = 10,
    TalentMatchingFiltersDto? Filters = null) : IRequest<TalentMatchingResultDto>;
