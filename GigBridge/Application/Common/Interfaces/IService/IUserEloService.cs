using Domain.Entities;

namespace Application.Common.Interfaces.IService;

public interface IUserEloService
{
    Task InitializeNewUserAsync(User user, CancellationToken cancellationToken);

    Task ApplyLoginActivityAsync(User user, CancellationToken cancellationToken);

    /// <summary>
    /// Applies the single piecewise Elo delta earned by <paramref name="revieweeId"/>
    /// once the contract is <see cref="Domain.Enums.ContractStatus.Completed"/> and a
    /// valid final review (<paramref name="rating"/>, 1.0–5.0 one decimal place) exists.
    /// Idempotent: at most one CompletedJobReview transaction per (reviewee, contract).
    /// No-op when the contract is not yet Completed or the reviewee is ineligible.
    /// </summary>
    Task ApplyCompletedJobReviewAsync(
        Guid reviewId,
        Guid contractId,
        Guid revieweeId,
        decimal rating,
        CancellationToken cancellationToken);

    Task<int> ApplyReviewModerationAsync(
        Guid reviewId,
        Guid revieweeId,
        Guid operationId,
        bool hide,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deducts 50% of <paramref name="userId"/>'s Elo points (rounded half-up) as a
    /// dispute-resolution penalty against a violating party. Idempotent per
    /// (dispute, user). No-op for ineligible roles or when there is nothing to deduct.
    /// </summary>
    Task ApplyDisputeResolutionPenaltyAsync(
        Guid userId,
        Guid disputeId,
        CancellationToken cancellationToken);
}
