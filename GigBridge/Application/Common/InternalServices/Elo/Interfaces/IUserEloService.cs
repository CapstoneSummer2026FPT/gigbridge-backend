using Domain.Entities;
using Domain.Enums.Contracts;
using Domain.Enums.Elo;

namespace Application.Common.InternalServices.Elo.Interfaces;
public interface IUserEloService
{
    Task InitializeNewUserAsync(User user, CancellationToken cancellationToken);

    Task ApplyLoginActivityAsync(User user, CancellationToken cancellationToken);

    /// <summary>
    /// Applies the single piecewise Elo delta earned by <paramref name="revieweeId"/>
    /// once the contract is <see cref="Domain.Enums.Contracts.ContractStatus.Completed"/> and a
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
    /// Deducts the configured dispute-resolution penalty (default 50% of current
    /// points, rounded half-up; policy read from PlatformSetting) from
    /// <paramref name="userId"/>. Idempotent per (dispute, user). No-op for
    /// ineligible roles or when there is nothing to deduct.
    /// </summary>
    Task ApplyDisputeResolutionPenaltyAsync(
        Guid userId,
        Guid disputeId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Applies a manual administrator Elo adjustment through the centralized ledger
    /// workflow. The delta's sign selects AdminIncrease/AdminDecrease; the change is
    /// idempotent per <paramref name="requestId"/> and attributed to
    /// <paramref name="adminId"/>. Returns the created transaction (null when the
    /// request was already applied).
    /// </summary>
    Task<UserEloPointTransaction?> ApplyAdminAdjustmentAsync(
        Guid adminId,
        Guid userId,
        int delta,
        string? note,
        Guid requestId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Writes the correction transaction for a resolved Elo appeal (FullReversal
    /// negates the original delta; PartialCorrection/CustomAdjustment use
    /// <paramref name="correctedDelta"/>; NoChange writes nothing). Idempotent per
    /// appeal. Returns the created transaction, or null when no correction applies.
    /// </summary>
    Task<UserEloPointTransaction?> ApplyAppealResolutionAsync(
        EloPointAppeal appeal,
        EloPointAppealResolution resolution,
        int? correctedDelta,
        Guid adminId,
        CancellationToken cancellationToken);
}
