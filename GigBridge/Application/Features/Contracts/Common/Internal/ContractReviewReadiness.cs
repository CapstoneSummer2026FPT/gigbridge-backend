using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Common.Internal;

internal readonly record struct ContractReviewState(
    bool CanReview,
    bool HasReviewedByCurrentUser);

internal static class ContractReviewReadiness
{
    public static async Task<ContractReviewState> GetStateAsync(
        IApplicationDbContext context,
        Contract contract,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var isParticipant = await IsParticipantAsync(context, contract, userId, cancellationToken);
        if (!isParticipant)
        {
            return new ContractReviewState(false, false);
        }

        var hasReviewed = await context.Set<Review>()
            .AsNoTracking()
            .AnyAsync(
                review =>
                    review.ContractsId == contract.ContractsId &&
                    review.ReviewerId == userId,
                cancellationToken);

        return new ContractReviewState(
            contract.Status == (int)ContractStatus.Completed && !hasReviewed,
            hasReviewed);
    }

    private static async Task<bool> IsParticipantAsync(
        IApplicationDbContext context,
        Contract contract,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var isClient = await context.Set<ClientProfile>()
            .AsNoTracking()
            .AnyAsync(
                profile =>
                    profile.UserId == userId &&
                    profile.ClientProfilesId == contract.ClientProfilesId,
                cancellationToken);

        if (isClient)
        {
            return true;
        }

        return contract.FreelancerProfilesId.HasValue &&
            await context.Set<FreelancerProfile>()
                .AsNoTracking()
                .AnyAsync(
                    profile =>
                        profile.UserId == userId &&
                        profile.FreelancerProfilesId == contract.FreelancerProfilesId.Value,
                    cancellationToken);
    }
}
