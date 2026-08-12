using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums.Contracts;
using Domain.Enums.Contracts.Escrow;
using Domain.Enums.ESign;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Common.Internal;

internal static class ContractEscrowReadiness
{
    private const int ClosedJobPostStatus = 2;

    public static async Task<EsignDocument> RequireFullySignedDocumentAsync(
        IApplicationDbContext context,
        Guid contractId,
        CancellationToken cancellationToken)
    {
        var document = await context.Set<EsignDocument>()
            .Where(document =>
                document.ContractsId == contractId &&
                document.Status == (int)ESignDocumentStatus.FullySigned)
            .OrderByDescending(document => document.FinalizedAt ?? document.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return document ?? throw new BadRequestException("Contract escrow can only be funded after both parties sign.");
    }

    public static async Task<ContractEscrow> EnsurePendingEscrowAsync(
        IApplicationDbContext context,
        Contract contract,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var milestones = await context.Set<Milestone>()
            .Include(milestone => milestone.WorkItems)
            .Where(milestone => milestone.ContractsId == contract.ContractsId)
            .ToListAsync(cancellationToken);

        ContractDetailsValidator.ValidateMilestonesForSubmitOrPublish(contract, milestones);

        var escrow = await context.Set<ContractEscrow>()
            .FirstOrDefaultAsync(escrow => escrow.ContractsId == contract.ContractsId, cancellationToken);

        if (escrow is null)
        {
            escrow = new ContractEscrow
            {
                ContractEscrowId = Guid.NewGuid(),
                ContractsId = contract.ContractsId,
                RequiredAmount = contract.TotalBudget,
                FundedAmount = 0m,
                RequiredPercentage = 1.0m,
                Currency = "VND",
                Status = (int)ContractEscrowStatus.PendingFunding,
                CreatedAt = now
            };
            context.Set<ContractEscrow>().Add(escrow);
        }
        else
        {
            escrow.RequiredAmount = contract.TotalBudget;
            escrow.FundedAmount = 0m;
            escrow.RequiredPercentage = 1.0m;
            escrow.Currency = string.IsNullOrWhiteSpace(escrow.Currency) ? "VND" : escrow.Currency;
            escrow.Status = (int)ContractEscrowStatus.PendingFunding;
            escrow.FundedAt = null;
            escrow.ReleasedAt = null;
        }

        contract.Status = (int)ContractStatus.PendingEscrow;
        contract.UpdatedAt = now;

        var jobPost = await context.Set<JobPost>()
            .FirstOrDefaultAsync(jobPost => jobPost.JobPostsId == contract.JobPostsId, cancellationToken);

        if (jobPost is not null)
        {
            jobPost.Status = ClosedJobPostStatus;
            jobPost.UpdatedAt = now;
        }

        return escrow;
    }
}
