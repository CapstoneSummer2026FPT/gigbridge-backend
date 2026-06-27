using Application.Features.Admin.Cheating.DTOs;
using MediatR;

namespace Application.Features.Admin.Cheating.GetViolations.Queries;

public record GetAdminCheatingViolationsQuery(
    int Page = 1,
    int PageSize = 20,
    int? Action = null,
    bool? IsReviewed = null,
    Guid? FreelancerUserId = null,
    Guid? ProposalId = null,
    DateTime? From = null,
    DateTime? To = null,
    string? Search = null) : IRequest<AdminCheatingViolationsResponse>;
