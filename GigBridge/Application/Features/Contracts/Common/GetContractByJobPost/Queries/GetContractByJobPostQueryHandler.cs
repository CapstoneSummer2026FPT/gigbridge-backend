using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Contracts.Common.GetContractByJobPost.DTOs;
using Application.Features.Contracts.Common.Internal;
using Application.Features.Contracts.ProductHandoffs.Common;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Chat;
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

        var jobPost = await _context.Set<JobPost>()
            .AsNoTracking()
            .FirstOrDefaultAsync(jp => jp.JobPostsId == contract.JobPostsId, cancellationToken);

        var clientProfile = await _context.Set<ClientProfile>()
            .AsNoTracking()
            .Include(cp => cp.User)
            .FirstOrDefaultAsync(cp => cp.ClientProfilesId == contract.ClientProfilesId, cancellationToken);
        var clientUser = clientProfile?.User;

        User? freelancerUser = null;
        if (contract.FreelancerProfilesId.HasValue)
        {
            var freelancerProfile = await _context.Set<FreelancerProfile>()
                .AsNoTracking()
                .Include(fp => fp.User)
                .FirstOrDefaultAsync(fp => fp.FreelancerProfilesId == contract.FreelancerProfilesId.Value, cancellationToken);
            freelancerUser = freelancerProfile?.User;
        }

        var conversationId = await _context.Set<Conversation>()
            .AsNoTracking()
            .Where(c => c.ContractsId == contract.ContractsId && c.ConversationType != (int)ConversationType.Dispute)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => (Guid?)c.ConversationsId)
            .FirstOrDefaultAsync(cancellationToken);

        var reviewState = await ContractReviewReadiness.GetStateAsync(
            _context,
            contract,
            request.UserId,
            cancellationToken);

        var currentProductHandoff = await _context.Set<ContractProductHandoff>()
            .AsNoTracking()
            .Where(handoff => handoff.ContractsId == contract.ContractsId && handoff.IsCurrent)
            .OrderByDescending(handoff => handoff.Version)
            .FirstOrDefaultAsync(cancellationToken);

        return ToResponse(contract, escrow, jobPost, clientUser, freelancerUser, conversationId, reviewState, currentProductHandoff);
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

    private static ContractDetailResponse ToResponse(
        Contract contract,
        ContractEscrow? escrow,
        JobPost? jobPost,
        User? clientUser,
        User? freelancerUser,
        Guid? conversationId,
        ContractReviewState reviewState,
        ContractProductHandoff? currentProductHandoff)
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
            Status = contract.Status,
            RevisionNumber = contract.RevisionNumber,
            StartDate = contract.StartDate,
            EndDate = contract.EndDate,
            CompletedAt = contract.CompletedAt,
            CreatedAt = contract.CreatedAt,
            UpdatedAt = contract.UpdatedAt,
            CanReview = reviewState.CanReview,
            HasReviewedByCurrentUser = reviewState.HasReviewedByCurrentUser,
            Escrow = escrow is null ? null : ContractEscrowResponseMapper.ToResponse(escrow),
            JobTitle = jobPost?.Title,
            JobDescription = jobPost?.Description,
            ClientName = clientUser?.FullName ?? "Client",
            ClientEmail = clientUser?.Email,
            FreelancerName = freelancerUser?.FullName,
            FreelancerEmail = freelancerUser?.Email,
            ClientUserId = clientUser?.UserId,
            FreelancerUserId = freelancerUser?.UserId,
            ConversationId = conversationId,
            CurrentProductHandoff = currentProductHandoff is null
                ? null
                : ContractProductHandoffMapper.ToResponse(currentProductHandoff)
        };
    }
}
