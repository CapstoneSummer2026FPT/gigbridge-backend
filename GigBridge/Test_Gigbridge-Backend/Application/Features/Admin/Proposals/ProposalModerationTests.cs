using Application.Common.Exceptions;
using Application.Features.Proposals.Common;
using Domain.Entities;
using Domain.Enums;

namespace Test_Gigbridge_Backend.Application.Features.Admin.Proposals;

public sealed class ProposalModerationTests
{
    [Fact]
    public void Guard_Allows_Active_Proposal()
    {
        var proposal = new Proposal { ModerationStatus = (int)ProposalModerationStatus.Active };
        ProposalModerationGuard.EnsureActive(proposal);
    }

    [Fact]
    public void Guard_Blocks_Invalidated_Proposal_Without_Changing_Lifecycle()
    {
        var proposal = new Proposal { Status = (int)ProposalStatus.Shortlisted, ModerationStatus = (int)ProposalModerationStatus.Invalidated };
        Assert.Throws<ConflictException>(() => ProposalModerationGuard.EnsureActive(proposal));
        Assert.Equal((int)ProposalStatus.Shortlisted, proposal.Status);
    }

    [Fact]
    public void Moderation_Enum_Is_Separate_From_Lifecycle_Enum()
    {
        Assert.Equal(0, (int)ProposalModerationStatus.Active);
        Assert.Equal(1, (int)ProposalModerationStatus.Invalidated);
        Assert.Equal(5, (int)ProposalStatus.Withdrawn);
    }
}
