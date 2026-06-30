using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Common.Internal;

internal static class JobPostSetupPublishGuard
{
    public const int DraftStatus = 0;
    public const int OpenStatus = 1;

    public static async Task EnsureCanPublishAsync(
        IApplicationDbContext context,
        JobPost jobPost,
        Contract? contract,
        CancellationToken cancellationToken)
    {
        if (jobPost.Status != DraftStatus && jobPost.Status != OpenStatus)
        {
            throw new BadRequestException("Job post must be in Draft or Open status to complete setup.");
        }

        if (contract is null)
        {
            throw new BadRequestException("Draft contract must exist before publishing the job post.");
        }

        var isEsignFullySigned = await context.Set<EsignDocument>()
            .AnyAsync(
                document =>
                    document.JobPostsId == jobPost.JobPostsId &&
                    document.Status == (int)ESignDocumentStatus.FullySigned,
                cancellationToken);

        if (!isEsignFullySigned)
        {
            throw new BadRequestException("Job post e-sign document is not fully signed.");
        }

        if (contract.Milestones.Count == 0)
        {
            throw new BadRequestException("At least one milestone is required.");
        }

        foreach (var milestone in contract.Milestones)
        {
            if (string.IsNullOrWhiteSpace(milestone.Title))
            {
                throw new BadRequestException("Milestone title cannot be empty.");
            }

            if (milestone.Amount <= 0)
            {
                throw new BadRequestException("Milestone amount must be positive.");
            }
        }

        ContractDetailsValidator.ValidateMilestoneTotalDoesNotExceedBudget(contract, contract.Milestones.ToList());
    }
}
