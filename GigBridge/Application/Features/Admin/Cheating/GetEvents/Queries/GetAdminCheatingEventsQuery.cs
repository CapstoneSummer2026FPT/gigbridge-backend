using Application.Features.Admin.Cheating.DTOs;
using MediatR;

namespace Application.Features.Admin.Cheating.GetEvents.Queries;

public record GetAdminCheatingEventsQuery(
    int Page = 1,
    int PageSize = 20,
    int? EventType = null,
    Guid? FreelancerUserId = null,
    Guid? ProposalId = null,
    DateTime? From = null,
    DateTime? To = null,
    string? Search = null) : IRequest<AdminCheatingEventsResponse>;
