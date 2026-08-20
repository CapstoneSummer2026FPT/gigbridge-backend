using System;
using System.Threading;
using System.Threading.Tasks;
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

namespace Application.Features.Contracts.Common.GetContractById.Queries;

public class GetContractByIdQueryHandler : IRequestHandler<GetContractByIdQuery, ContractDetailResponse>
{
    private readonly IApplicationDbContext _context;

    public GetContractByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ContractDetailResponse> Handle(GetContractByIdQuery request, CancellationToken cancellationToken)
    {
        // Combined into a single round trip: Contract, ContractEscrow, JobPost, ClientProfile+User,
        // and FreelancerProfile+User are all single-valued (non-collection) navigations, so
        // eager-loading them together is a plain set of LEFT JOINs with no row-multiplication risk
        // — this replaces what used to be 5 separate sequential queries.
        var contract = await _context.Set<Contract>()
            .AsNoTracking()
            .Include(c => c.ContractEscrow)
            .Include(c => c.JobPosts)
            .Include(c => c.ClientProfiles).ThenInclude(cp => cp.User)
            .Include(c => c.FreelancerProfiles).ThenInclude(fp => fp!.User)
            .FirstOrDefaultAsync(c => c.ContractsId == request.ContractId, cancellationToken);

        if (contract is null)
        {
            throw new NotFoundException("Contract does not exist.");
        }

        await EnsureCanViewContract(contract, request.UserId, cancellationToken);

        var escrow = contract.ContractEscrow;
        var jobPost = contract.JobPosts;
        var clientUser = contract.ClientProfiles?.User;
        var freelancerUser = contract.FreelancerProfiles?.User;

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

    private async Task EnsureCanViewContract(Contract contract, Guid userId, CancellationToken cancellationToken)
    {
        var user = await _context.Set<User>()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user is null)
        {
            throw new ForbiddenAccessException("You do not have permission to view this contract.");
        }

        if (user.Role == (int)UserRole.Admin)
        {
            return;
        }

        var isOwnerClientQuery = _context.Set<ClientProfile>()
            .AsNoTracking()
            .Where(cp => cp.UserId == userId && cp.ClientProfilesId == contract.ClientProfilesId)
            .Select(cp => 1);

        bool isParticipant;
        if (contract.FreelancerProfilesId.HasValue)
        {
            // Combined into a single SQL UNION round trip instead of two sequential AnyAsync calls.
            var freelancerProfileId = contract.FreelancerProfilesId.Value;
            var isAttachedFreelancerQuery = _context.Set<FreelancerProfile>()
                .AsNoTracking()
                .Where(fp => fp.UserId == userId && fp.FreelancerProfilesId == freelancerProfileId)
                .Select(fp => 1);
            isParticipant = await isOwnerClientQuery.Union(isAttachedFreelancerQuery).AnyAsync(cancellationToken);
        }
        else
        {
            isParticipant = await isOwnerClientQuery.AnyAsync(cancellationToken);
        }

        if (isParticipant)
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
