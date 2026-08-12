using Application.Common.Interfaces;
using Application.Features.Reviews.Admin.DTOs;
using Application.Features.Reviews.Common;
using Domain.Entities;
using Domain.Enums.Reports;
using Domain.Enums.Reviews;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Reviews.Admin.GetReviews.Queries;

public sealed class GetAdminReviewsQueryHandler : IRequestHandler<GetAdminReviewsQuery, AdminReviewsResponse>
{
    private readonly IApplicationDbContext _context;

    public GetAdminReviewsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AdminReviewsResponse> Handle(GetAdminReviewsQuery query, CancellationToken cancellationToken)
    {
        var allReviews = _context.Set<Review>().AsNoTracking();
        var openReports = _context.Set<Report>().AsNoTracking().Where(report =>
            report.ReportedEntityType == ReportedEntityTypes.Review &&
            (report.Status == (int)ReportStatus.Pending || report.Status == (int)ReportStatus.Reviewing));

        var filtered = allReviews
            .Include(review => review.Contracts)
            .Include(review => review.Reviewer)
            .Include(review => review.Reviewee)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            filtered = filtered.Where(review =>
                review.Contracts.Title.ToLower().Contains(search) ||
                review.Reviewer.FullName.ToLower().Contains(search) ||
                review.Reviewee.FullName.ToLower().Contains(search));
        }
        if (query.Rating.HasValue) filtered = filtered.Where(review => review.Rating == query.Rating.Value);
        if (query.ReviewerRole.HasValue) filtered = filtered.Where(review => review.Reviewer.Role == query.ReviewerRole.Value);
        if (query.RevieweeRole.HasValue) filtered = filtered.Where(review => review.Reviewee.Role == query.RevieweeRole.Value);
        if (query.ModerationStatus.HasValue) filtered = filtered.Where(review => review.ModerationStatus == (int)query.ModerationStatus.Value);
        if (query.HasOpenReport.HasValue)
        {
            filtered = query.HasOpenReport.Value
                ? filtered.Where(review => openReports.Any(report => report.ReportedEntityId == review.ReviewsId))
                : filtered.Where(review => !openReports.Any(report => report.ReportedEntityId == review.ReviewsId));
        }

        var totalItems = await filtered.CountAsync(cancellationToken);
        var reviews = await filtered
            .OrderByDescending(review => review.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
        var reviewIds = reviews.Select(review => review.ReviewsId).ToArray();
        var reportCounts = await _context.Set<Report>()
            .AsNoTracking()
            .Where(report => report.ReportedEntityType == ReportedEntityTypes.Review && reviewIds.Contains(report.ReportedEntityId))
            .GroupBy(report => report.ReportedEntityId)
            .Select(group => new
            {
                ReviewId = group.Key,
                Total = group.Count(),
                Open = group.Count(report => report.Status == (int)ReportStatus.Pending || report.Status == (int)ReportStatus.Reviewing)
            })
            .ToListAsync(cancellationToken);
        var countsByReview = reportCounts.ToDictionary(item => item.ReviewId);

        var total = await allReviews.CountAsync(cancellationToken);
        var hidden = await allReviews.CountAsync(
            review => review.ModerationStatus == (int)ReviewModerationStatus.Hidden,
            cancellationToken);
        var withOpenReports = await openReports
            .Select(report => report.ReportedEntityId)
            .Distinct()
            .CountAsync(cancellationToken);

        return new AdminReviewsResponse
        {
            Items = reviews.Select(review =>
            {
                countsByReview.TryGetValue(review.ReviewsId, out var counts);
                return ReviewManagementProjection.ToDto(
                    review,
                    revealAnonymousReviewer: true,
                    hasOpenReport: counts?.Open > 0,
                    openReportCount: counts?.Open ?? 0,
                    totalReportCount: counts?.Total ?? 0);
            }).ToList(),
            Summary = new AdminReviewSummaryDto
            {
                Total = total,
                Active = total - hidden,
                Hidden = hidden,
                WithOpenReports = withOpenReports
            },
            Page = query.Page,
            PageSize = query.PageSize,
            TotalItems = totalItems
        };
    }
}
