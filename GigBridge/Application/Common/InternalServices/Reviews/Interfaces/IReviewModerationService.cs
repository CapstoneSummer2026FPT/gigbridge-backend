using Application.Common.InternalServices.Reviews.Models;
using Domain.Enums.Reviews;

namespace Application.Common.InternalServices.Reviews.Interfaces;
public interface IReviewModerationService
{
    Task<ReviewModerationResult> SetStatusAsync(
        Guid reviewId,
        ReviewModerationStatus status,
        Guid adminId,
        string note,
        CancellationToken cancellationToken);
}
