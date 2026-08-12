using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums.Accounts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.JobPosts.Delete.Commands;

public sealed record HardDeleteJobPostCommand(
    Guid AdminUserId,
    Guid JobPostId) : IRequest<bool>;

public sealed class HardDeleteJobPostCommandHandler :
    IRequestHandler<HardDeleteJobPostCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public HardDeleteJobPostCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(
        HardDeleteJobPostCommand request,
        CancellationToken cancellationToken)
    {
        var jobPost = await _context.Set<JobPost>()
            .Include(jp => jp.JobPostAttachments)
            .Include(jp => jp.JobPostSkills)
            .Include(jp => jp.JobPostQuestions)
            .Include(jp => jp.SavedJobs)
            .Include(jp => jp.JobInvitations)
            .Include(jp => jp.Proposals)
                .ThenInclude(p => p.ProposalAnswers)
            .AsSplitQuery()
            .Where(jp => jp.JobPostsId == request.JobPostId)
            .Where(_ => _context.Set<User>().Any(user =>
                user.UserId == request.AdminUserId && user.Role == (int)UserRole.Admin))
            .FirstOrDefaultAsync(cancellationToken);

        if (jobPost is null)
        {
            var isAdmin = await _context.Set<User>()
                .AsNoTracking()
                .AnyAsync(user =>
                    user.UserId == request.AdminUserId && user.Role == (int)UserRole.Admin,
                    cancellationToken);

            if (!isAdmin)
            {
                throw new ForbiddenAccessException("Only admins can perform a hard delete on job posts.");
            }

            throw new NotFoundException("Job post does not exist.");
        }

        // Clean up related entities to prevent foreign key errors (cascade relationships)

        // 1. Clean up ESignDocuments & EsignSignatures
        var esignDocs = await _context.Set<EsignDocument>()
            .Include(ed => ed.EsignSignatures)
            .Where(ed => ed.JobPostsId == request.JobPostId)
            .ToListAsync(cancellationToken);
        
        foreach (var ed in esignDocs)
        {
            _context.Set<EsignSignature>().RemoveRange(ed.EsignSignatures);
        }
        _context.Set<EsignDocument>().RemoveRange(esignDocs);

        // 2. Clean up NegotiationOffers
        var negotiationOffers = await _context.Set<NegotiationOffer>()
            .Where(no => no.JobPostsId == request.JobPostId)
            .ToListAsync(cancellationToken);
        _context.Set<NegotiationOffer>().RemoveRange(negotiationOffers);

        // 3. Clean up Contracts and all their dependent entities
        var contracts = await _context.Set<Contract>()
            .Where(c => c.JobPostsId == request.JobPostId)
            .ToListAsync(cancellationToken);

        var contractIds = contracts.Select(contract => contract.ContractsId).ToList();

        if (contractIds.Count > 0)
        {
            // Milestone attachments
            var milestones = await _context.Set<Milestone>()
                .Include(m => m.MilestoneAttachments)
                .Where(m => contractIds.Contains(m.ContractsId))
                .ToListAsync(cancellationToken);

            foreach (var m in milestones)
            {
                _context.Set<MilestoneAttachment>().RemoveRange(m.MilestoneAttachments);
            }

            // Escrow transactions and contract escrows
            var contractEscrows = await _context.Set<ContractEscrow>()
                .Include(ce => ce.EscrowTransactions)
                .Where(ce => contractIds.Contains(ce.ContractsId))
                .ToListAsync(cancellationToken);

            var contractEscrowIds = contractEscrows
                .Select(contractEscrow => contractEscrow.ContractEscrowId)
                .ToList();

            if (contractEscrowIds.Count > 0)
            {
                // Nullify references in wallet transactions first
                var walletTxnsEscrow = await _context.Set<WalletTransaction>()
                    .Where(wt => wt.ContractEscrowId.HasValue &&
                        contractEscrowIds.Contains(wt.ContractEscrowId.Value))
                    .ToListAsync(cancellationToken);
                foreach (var wt in walletTxnsEscrow)
                {
                    wt.ContractEscrowId = null;
                }
            }

            foreach (var ce in contractEscrows)
            {
                _context.Set<EscrowTransaction>().RemoveRange(ce.EscrowTransactions);
            }
            _context.Set<ContractEscrow>().RemoveRange(contractEscrows);

            // Disputes and dispute evidences
            var disputes = await _context.Set<Dispute>()
                .Include(d => d.DisputeEvidences)
                .Where(d => contractIds.Contains(d.ContractsId))
                .ToListAsync(cancellationToken);

            foreach (var d in disputes)
            {
                _context.Set<DisputeEvidence>().RemoveRange(d.DisputeEvidences);
            }
            _context.Set<Dispute>().RemoveRange(disputes);

            // Contract product handoffs
            var handoffs = await _context.Set<ContractProductHandoff>()
                .Where(h => contractIds.Contains(h.ContractsId))
                .ToListAsync(cancellationToken);
            _context.Set<ContractProductHandoff>().RemoveRange(handoffs);

            // Reviews
            var reviews = await _context.Set<Review>()
                .Where(r => contractIds.Contains(r.ContractsId))
                .ToListAsync(cancellationToken);
            _context.Set<Review>().RemoveRange(reviews);

            // Nullify or clean up wallet transactions referencing the contract
            var walletTxnsContract = await _context.Set<WalletTransaction>()
                .Where(wt => wt.ContractsId.HasValue && contractIds.Contains(wt.ContractsId.Value))
                .ToListAsync(cancellationToken);
            foreach (var wt in walletTxnsContract)
            {
                wt.ContractsId = null;
            }

            // Finally remove milestones
            _context.Set<Milestone>().RemoveRange(milestones);
        }
        _context.Set<Contract>().RemoveRange(contracts);

        // 4. Clean up Conversations, Messages, Participants, Schedules
        var conversations = await _context.Set<Conversation>()
            .Include(c => c.Messages)
                .ThenInclude(m => m.MessageAttachments)
            .Include(c => c.Participants)
            .Include(c => c.Schedules)
                .ThenInclude(s => s.MeetProvisioningJobs)
            .Where(c => c.JobPostsId == request.JobPostId)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        // Break circular last message ID reference first
        foreach (var c in conversations)
        {
            c.LastMessageId = null;
        }
        await _context.SaveChangesAsync(cancellationToken);

        foreach (var c in conversations)
        {
            foreach (var m in c.Messages)
            {
                _context.Set<MessageAttachment>().RemoveRange(m.MessageAttachments);
            }
            _context.Set<Message>().RemoveRange(c.Messages);
            _context.Set<ConversationParticipant>().RemoveRange(c.Participants);

            foreach (var s in c.Schedules)
            {
                _context.Set<GoogleMeetProvisioningJob>().RemoveRange(s.MeetProvisioningJobs);
            }
            _context.Set<Schedule>().RemoveRange(c.Schedules);
        }
        _context.Set<Conversation>().RemoveRange(conversations);

        // 5. Clean up other standard JobPost dependent entities
        _context.Set<JobPostAttachment>().RemoveRange(jobPost.JobPostAttachments);
        _context.Set<JobPostSkill>().RemoveRange(jobPost.JobPostSkills);
        _context.Set<JobPostQuestion>().RemoveRange(jobPost.JobPostQuestions);
        _context.Set<SavedJob>().RemoveRange(jobPost.SavedJobs);
        _context.Set<JobInvitation>().RemoveRange(jobPost.JobInvitations);

        foreach (var proposal in jobPost.Proposals)
        {
            _context.Set<ProposalAnswer>().RemoveRange(proposal.ProposalAnswers);
        }
        _context.Set<Proposal>().RemoveRange(jobPost.Proposals);

        // Remove the job post itself
        _context.Set<JobPost>().Remove(jobPost);

        await _context.SaveChangesAsync(cancellationToken);

        // Per user request, this action does NOT write to AdminAuditLog or leave any trace.

        return true;
    }
}
