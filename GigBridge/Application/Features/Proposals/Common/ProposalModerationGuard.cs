using Domain.Enums.Proposals;
using Application.Common.Exceptions;
using Domain.Entities;


namespace Application.Features.Proposals.Common;

public static class ProposalModerationGuard
{
    public static void EnsureActive(Proposal proposal)
    {
        if (proposal.ModerationStatus == (int)ProposalModerationStatus.Invalidated)
            throw new ConflictException("This proposal was invalidated by an administrator and cannot continue until it is restored.");
    }
}
