using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Contracts.Common.GetContractByJobPost.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Common.GetContractByJobPost.Queries;

public class GetContractByJobPostQueryHandler
    : IRequestHandler<GetContractByJobPostQuery, ContractDetailResponse>
{
    private readonly IApplicationDbContext _context;

    public GetContractByJobPostQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ContractDetailResponse> Handle(
        GetContractByJobPostQuery request,
        CancellationToken cancellationToken)
    {
        var contract = await _context.Set<Contract>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                contract => contract.JobPostsId == request.JobPostId,
                cancellationToken);

        if (contract is null)
        {
            throw new NotFoundException("Contract does not exist for this job post.");
        }

        await EnsureCanViewContract(contract, request.UserId, cancellationToken);

        var escrow = await _context.Set<ContractEscrow>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                escrow => escrow.ContractsId == contract.ContractsId,
                cancellationToken);

        return ToResponse(contract, escrow);
    }

    private async Task EnsureCanViewContract(
        Contract contract,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await _context.Set<User>()
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.UserId == userId, cancellationToken);

        if (user is null)
        {
            throw new ForbiddenAccessException("You do not have permission to view this contract.");
        }

        if (user.Role == (int)UserRole.Admin)
        {
            return;
        }

        var isOwnerClient = await _context.Set<ClientProfile>()
            .AsNoTracking()
            .AnyAsync(
                profile =>
                    profile.UserId == userId &&
                    profile.ClientProfilesId == contract.ClientProfilesId,
                cancellationToken);

        if (isOwnerClient)
        {
            return;
        }

        var isAttachedFreelancer = contract.FreelancerProfilesId.HasValue &&
            await _context.Set<FreelancerProfile>()
                .AsNoTracking()
                .AnyAsync(
                    profile =>
                        profile.UserId == userId &&
                        profile.FreelancerProfilesId == contract.FreelancerProfilesId.Value,
                    cancellationToken);

        if (isAttachedFreelancer)
        {
            return;
        }

        throw new ForbiddenAccessException("You do not have permission to view this contract.");
    }

    private static ContractDetailResponse ToResponse(Contract contract, ContractEscrow? escrow)
    {
        return new ContractDetailResponse
        {
            ContractId = contract.ContractsId,
            JobPostId = contract.JobPostsId,
            ClientProfileId = contract.ClientProfilesId,
            FreelancerProfileId = contract.FreelancerProfilesId,
            ProposalId = contract.ProposalsId,
            Title = contract.Title,
            Description = contract.Description,
            TotalBudget = contract.TotalBudget,
            DisputeTerms = contract.DisputeTerms,
            Status = contract.Status,
            StartDate = contract.StartDate,
            EndDate = contract.EndDate,
            CreatedAt = contract.CreatedAt,
            UpdatedAt = contract.UpdatedAt,
            Escrow = escrow is null
                ? null
                : new ContractEscrowResponse
                {
                    ContractEscrowId = escrow.ContractEscrowId,
                    RequiredAmount = escrow.RequiredAmount,
                    FundedAmount = escrow.FundedAmount,
                    ReleasedAmount = escrow.ReleasedAmount,
                    RequiredPercentage = escrow.RequiredPercentage,
                    Currency = escrow.Currency,
                    Status = escrow.Status,
                    CreatedAt = escrow.CreatedAt,
                    FundedAt = escrow.FundedAt
                }
        };
    }
}
