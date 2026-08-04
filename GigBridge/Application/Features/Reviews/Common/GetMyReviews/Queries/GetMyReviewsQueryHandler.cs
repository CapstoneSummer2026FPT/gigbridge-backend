using Application.Common.Interfaces;
using Application.Features.Reviews.Common.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Reviews.Common.GetMyReviews.Queries;

public sealed class GetMyReviewsQueryHandler : IRequestHandler<GetMyReviewsQuery, MyReviewsResponse>
{
    private readonly IApplicationDbContext _context;

    public GetMyReviewsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<MyReviewsResponse> Handle(GetMyReviewsQuery query, CancellationToken cancellationToken)
    {
        var received = query.Direction.Equals("received", StringComparison.OrdinalIgnoreCase);
        var reviewsQuery = _context.Set<Review>()
            .AsNoTracking()
            .Include(review => review.Contracts)
            .Include(review => review.Reviewer)
            .Include(review => review.Reviewee)
            .Where(review => received
                ? review.RevieweeId == query.UserId
                : review.ReviewerId == query.UserId);

        var totalItems = await reviewsQuery.CountAsync(cancellationToken);
        var reviews = await reviewsQuery
            .OrderByDescending(review => review.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var reviewIds = reviews.Select(review => review.ReviewsId).ToArray();
        var openReportedReviewIds = received
            ? await _context.Set<Report>()
                .AsNoTracking()
                .Where(report =>
                    report.ReporterId == query.UserId &&
                    report.ReportedEntityType == ReportedEntityTypes.Review &&
                    reviewIds.Contains(report.ReportedEntityId) &&
                    (report.Status == (int)ReportStatus.Pending || report.Status == (int)ReportStatus.Reviewing))
                .Select(report => report.ReportedEntityId)
                .Distinct()
                .ToListAsync(cancellationToken)
            : [];
        var openReportSet = openReportedReviewIds.ToHashSet();

        return new MyReviewsResponse
        {
            Items = reviews
                .Select(review => ReviewManagementProjection.ToDto(
                    review,
                    revealAnonymousReviewer: false,
                    hasOpenReport: openReportSet.Contains(review.ReviewsId)))
                .ToList(),
            Page = query.Page,
            PageSize = query.PageSize,
            TotalItems = totalItems
        };
    }
}
