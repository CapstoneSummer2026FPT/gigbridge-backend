using Application.Features.Reviews.Common.DTOs;
using Domain.Entities;
using Domain.Enums;

namespace Application.Features.Reviews.Common;

internal static class ReviewManagementProjection
{
    public static ManagedReviewDto ToDto(
        Review review,
        bool revealAnonymousReviewer,
        bool hasOpenReport = false,
        int openReportCount = 0,
        int totalReportCount = 0)
    {
        var isAnonymous = review.IsVisible == false;
        return new ManagedReviewDto
        {
            ReviewId = review.ReviewsId,
            ContractId = review.ContractsId,
            JobPostId = review.Contracts.JobPostsId,
            ProjectTitle = review.Contracts.Title,
            ReviewerId = review.ReviewerId,
            ReviewerName = isAnonymous && !revealAnonymousReviewer
                ? "Anonymous User"
                : review.Reviewer.FullName,
            ReviewerRole = review.Reviewer.Role,
            RevieweeId = review.RevieweeId,
            RevieweeName = review.Reviewee.FullName,
            RevieweeRole = review.Reviewee.Role,
            Rating = review.Rating,
            Comment = review.Comment,
            CommunicationRating = review.CommunicationRating,
            QualityRating = review.QualityRating,
            TimelinessRating = review.TimelinessRating,
            IsAnonymous = isAnonymous,
            ModerationStatus = (ReviewModerationStatus)review.ModerationStatus,
            HasOpenReport = hasOpenReport,
            OpenReportCount = openReportCount,
            TotalReportCount = totalReportCount,
            CreatedAt = review.CreatedAt
        };
    }
}
