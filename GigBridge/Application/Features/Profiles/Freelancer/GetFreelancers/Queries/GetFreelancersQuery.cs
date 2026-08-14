using Application.Common.Models;
using Application.Features.Profiles.FreelancerProfile.GetFreelancers.DTOs;
using MediatR;

namespace Application.Features.Profiles.FreelancerProfile.GetFreelancers.Queries;

public sealed record GetFreelancersQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    List<string>? Skills = null,
    string? AvailabilityStatus = null,
    double? MinRating = null,
    string? Sort = null,
    bool SearchEngineVisibleOnly = false) : IRequest<PaginatedList<FreelancerSummaryDto>>;
