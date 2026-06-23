using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Common.Internal;

public static class JobPostDraftContractHelper
{
    public static async Task EnsureDraftContractForJobPostAsync(
        IApplicationDbContext context,
        JobPost jobPost,
        DateTime now,
        CancellationToken cancellationToken)
    {
        // Check if a contract for this JobPost already exists with no freelancer selected
        var existingContract = await context.Set<Contract>()
            .FirstOrDefaultAsync(c => c.JobPostsId == jobPost.JobPostsId && c.FreelancerProfilesId == null, cancellationToken);

        var budget = jobPost.BudgetMax ?? jobPost.BudgetMin ?? 0m;

        if (existingContract == null)
        {
            var newContract = new Contract
            {
                ContractsId = Guid.NewGuid(),
                JobPostsId = jobPost.JobPostsId,
                ClientProfilesId = jobPost.ClientProfilesId,
                FreelancerProfilesId = null,
                ProposalsId = null,
                Title = jobPost.Title,
                Description = jobPost.Description,
                TotalBudget = budget,
                Status = (int)ContractStatus.PendingFreelancerSelection, // 1
                CreatedAt = now,
                UpdatedAt = now
            };

            context.Set<Contract>().Add(newContract);
        }
        else
        {
            existingContract.Title = jobPost.Title;
            existingContract.Description = jobPost.Description;
            existingContract.TotalBudget = budget;
            existingContract.UpdatedAt = now;
        }
    }
}
