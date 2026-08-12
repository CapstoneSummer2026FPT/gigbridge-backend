using Application.Common.Interfaces;
using Application.Features.Reports.Common.DTOs;
using Domain.Entities;
using Domain.Enums.Reports;
using Domain.Enums.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Reports.Common;

public static class ReportProjection
{
    public static async Task<List<ReportDto>> ToDtosAsync(
        IApplicationDbContext context,
        IReadOnlyCollection<Report> reports,
        CancellationToken cancellationToken)
    {
        var targetSummaries = await GetTargetSummariesAsync(context, reports, cancellationToken);
        var reporterIds = reports.Select(report => report.ReporterId).Distinct().ToList();
        var now = DateTime.UtcNow;
        var premiumReporterIds = await context.Set<Subscription>()
            .AsNoTracking()
            .Where(subscription =>
                reporterIds.Contains(subscription.UserId) &&
                subscription.Status == SubscriptionStatus.Active &&
                subscription.StartDate <= now &&
                subscription.EndDate > now)
            .Select(subscription => subscription.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var premiumReporterSet = premiumReporterIds.ToHashSet();

        return reports.Select(report =>
        {
            targetSummaries.TryGetValue((report.ReportedEntityType, report.ReportedEntityId), out var targetSummary);

            return new ReportDto
            {
                Id = report.ReportsId,
                Reporter = new ReportUserSummaryDto
                {
                    Id = report.Reporter.UserId,
                    FullName = report.Reporter.FullName,
                    Email = report.Reporter.Email,
                    Role = report.Reporter.Role
                },
                ReportedEntityId = report.ReportedEntityId,
                ReportedEntityType = report.ReportedEntityType,
                Type = (ReportType)report.Type,
                Status = (ReportStatus)report.Status,
                IsPremiumReporter = premiumReporterSet.Contains(report.ReporterId),
                Reason = report.Reason,
                Description = report.Description,
                AdminNote = report.AdminNote,
                ResolvedByAdmin = report.ResolvedByAdmin is null
                    ? null
                    : new ReportUserSummaryDto
                    {
                        Id = report.ResolvedByAdmin.UserId,
                        FullName = report.ResolvedByAdmin.FullName,
                        Email = report.ResolvedByAdmin.Email,
                        Role = report.ResolvedByAdmin.Role
                    },
                TargetSummary = targetSummary,
                CreatedAt = report.CreatedAt,
                UpdatedAt = report.UpdatedAt,
                ResolvedAt = report.ResolvedAt
            };
        }).ToList();
    }

    public static async Task<ReportDto> ToDtoAsync(
        IApplicationDbContext context,
        Report report,
        CancellationToken cancellationToken)
    {
        var reports = await ToDtosAsync(context, [report], cancellationToken);
        return reports[0];
    }

    private static async Task<Dictionary<(string EntityType, Guid Id), ReportTargetSummaryDto>> GetTargetSummariesAsync(
        IApplicationDbContext context,
        IReadOnlyCollection<Report> reports,
        CancellationToken cancellationToken)
    {
        var summaries = new Dictionary<(string EntityType, Guid Id), ReportTargetSummaryDto>();

        var userIds = GetTargetIds(reports, ReportedEntityTypes.User);
        if (userIds.Count > 0)
        {
            var users = await context.Set<User>()
                .AsNoTracking()
                .Where(user => userIds.Contains(user.UserId))
                .Select(user => new ReportTargetSummaryDto
                {
                    Id = user.UserId,
                    EntityType = ReportedEntityTypes.User,
                    Title = user.FullName,
                    Email = user.Email,
                    Role = user.Role
                })
                .ToListAsync(cancellationToken);

            foreach (var user in users)
            {
                summaries[(ReportedEntityTypes.User, user.Id)] = user;
            }
        }

        var jobPostIds = GetTargetIds(reports, ReportedEntityTypes.JobPost);
        if (jobPostIds.Count > 0)
        {
            var jobPosts = await context.Set<JobPost>()
                .AsNoTracking()
                .Where(jobPost => jobPostIds.Contains(jobPost.JobPostsId))
                .Select(jobPost => new ReportTargetSummaryDto
                {
                    Id = jobPost.JobPostsId,
                    EntityType = ReportedEntityTypes.JobPost,
                    Title = jobPost.Title,
                    Description = jobPost.Description
                })
                .ToListAsync(cancellationToken);

            foreach (var jobPost in jobPosts)
            {
                summaries[(ReportedEntityTypes.JobPost, jobPost.Id)] = jobPost;
            }
        }

        var reviewIds = GetTargetIds(reports, ReportedEntityTypes.Review);
        if (reviewIds.Count > 0)
        {
            var reviews = await context.Set<Review>()
                .AsNoTracking()
                .Where(review => reviewIds.Contains(review.ReviewsId))
                .Select(review => new ReportTargetSummaryDto
                {
                    Id = review.ReviewsId,
                    EntityType = ReportedEntityTypes.Review,
                    Title = "Review",
                    Description = review.Comment,
                    Rating = review.Rating
                })
                .ToListAsync(cancellationToken);

            foreach (var review in reviews)
            {
                summaries[(ReportedEntityTypes.Review, review.Id)] = review;
            }
        }

        return summaries;
    }

    private static List<Guid> GetTargetIds(IReadOnlyCollection<Report> reports, string entityType)
    {
        return reports
            .Where(report => string.Equals(report.ReportedEntityType, entityType, StringComparison.OrdinalIgnoreCase))
            .Select(report => report.ReportedEntityId)
            .Distinct()
            .ToList();
    }
}
