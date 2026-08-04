using Application.Features.Reviews.Common.DTOs;
using Domain.Entities;

namespace Application.Features.Reviews.Common;

internal static class ReviewProjection
{
    public static ReviewDto ToDto(Review review)
    {
        var isVisible = review.IsVisible ?? true;

        return new ReviewDto
        {
            ReviewId = review.ReviewsId,
            ContractId = review.ContractsId,
            JobPostId = review.Contracts.JobPostsId,
            ProjectTitle = review.Contracts.Title,
            ReviewerId = review.ReviewerId,
            ReviewerName = isVisible ? review.Reviewer.FullName : "Anonymous User",
            RevieweeId = review.RevieweeId,
            Rating = review.Rating,
            Comment = review.Comment,
            CommunicationRating = review.CommunicationRating,
            QualityRating = review.QualityRating,
            TimelinessRating = review.TimelinessRating,
            IsVisible = isVisible,
            CreatedAt = review.CreatedAt
        };
    }
}
