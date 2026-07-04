using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.Proposals.Delete.Commands;

public sealed record HardDeleteProposalCommand(
    Guid AdminUserId,
    Guid ProposalId) : IRequest<bool>;

public sealed class HardDeleteProposalCommandHandler :
    IRequestHandler<HardDeleteProposalCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public HardDeleteProposalCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(
        HardDeleteProposalCommand request,
        CancellationToken cancellationToken)
    {
        var admin = await _context.Set<User>()
            .FirstOrDefaultAsync(user => user.UserId == request.AdminUserId, cancellationToken);

        if (admin is null || admin.Role != (int)UserRole.Admin)
        {
            throw new ForbiddenAccessException("Only admins can perform a hard delete on proposals.");
        }

        var proposal = await _context.Set<Proposal>()
            .Include(p => p.ProposalAnswers)
            .Include(p => p.ProposalAttachments)
            .FirstOrDefaultAsync(p => p.ProposalsId == request.ProposalId, cancellationToken);

        if (proposal is null)
        {
            throw new NotFoundException("Proposal does not exist.");
        }

        // Clean up related entities
        _context.Set<ProposalAnswer>().RemoveRange(proposal.ProposalAnswers);
        _context.Set<ProposalAttachment>().RemoveRange(proposal.ProposalAttachments);

        var cheatingEvents = await _context.Set<ProposalCheatingEvent>()
            .Where(e => e.ProposalsId == request.ProposalId)
            .ToListAsync(cancellationToken);
        _context.Set<ProposalCheatingEvent>().RemoveRange(cheatingEvents);


        // Remove from other dependencies if any (like timers or interview sessions)
        var timer = await _context.Set<ProposalQuestionTimer>()
            .FirstOrDefaultAsync(t => t.ProposalsId == request.ProposalId, cancellationToken);
        if (timer is not null)
        {
            _context.Set<ProposalQuestionTimer>().Remove(timer);
        }

        var session = await _context.Set<ProposalInterviewReviewSession>()
            .FirstOrDefaultAsync(s => s.ProposalsId == request.ProposalId, cancellationToken);
        if (session is not null)
        {
            _context.Set<ProposalInterviewReviewSession>().Remove(session);
        }

        _context.Set<Proposal>().Remove(proposal);

        await _context.SaveChangesAsync(cancellationToken);


        return true;
    }
}
