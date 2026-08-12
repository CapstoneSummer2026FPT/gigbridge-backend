using Application.Features.Reviews.Admin.DTOs;
using Domain.Enums.Reviews;
using MediatR;

namespace Application.Features.Reviews.Admin.GetReviews.Queries;

public sealed record GetAdminReviewsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    int? Rating = null,
    int? ReviewerRole = null,
    int? RevieweeRole = null,
    ReviewModerationStatus? ModerationStatus = null,
    bool? HasOpenReport = null) : IRequest<AdminReviewsResponse>;
