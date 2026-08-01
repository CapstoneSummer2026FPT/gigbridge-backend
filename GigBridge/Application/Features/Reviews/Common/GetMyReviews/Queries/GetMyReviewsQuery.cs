using Application.Features.Reviews.Common.DTOs;
using MediatR;

namespace Application.Features.Reviews.Common.GetMyReviews.Queries;

public sealed record GetMyReviewsQuery(
    Guid UserId,
    string Direction,
    int Page = 1,
    int PageSize = 10) : IRequest<MyReviewsResponse>;
