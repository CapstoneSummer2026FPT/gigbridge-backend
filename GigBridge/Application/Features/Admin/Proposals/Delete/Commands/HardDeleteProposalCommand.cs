using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums.Accounts;
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
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProposalsId == request.ProposalId, cancellationToken);

        if (proposal is null)
        {
            throw new NotFoundException("Proposal does not exist.");
        }

        throw new ConflictException(
            "Proposal hard deletion is disabled to preserve authored content and business history. Use Admin invalidation instead.");
    }
}
