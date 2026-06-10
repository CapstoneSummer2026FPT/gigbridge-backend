using Application.Common.Exceptions;
using Application.Features.Proposals.Freelancer.UpdateProposal.Commands;
using Application.Features.Proposals.Freelancer.UpdateProposal.DTOs;
using Domain.Enums;
using Test_Gigbridge_Backend.Support;

namespace Test_Gigbridge_Backend.Application.Features.Proposals.Freelancer;

public class UpdateProposalCommandHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsTrue_WhenProposalIsPendingAndBelongsToFreelancer()
    {
        await using var context = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();
        var freelancerProfile = TestData.FreelancerProfile(userId);
        var jobPost = TestData.JobPost(Guid.NewGuid());
        var proposal = TestData.Proposal(jobPost.JobPostsId, freelancerProfile.FreelancerProfilesId);
        context.Users.Add(TestData.User(userId, UserRole.Freelancer));
        context.FreelancerProfiles.Add(freelancerProfile);
        context.JobPosts.Add(jobPost);
        context.Proposals.Add(proposal);
        await context.SaveChangesAsync();
        var now = new DateTime(2026, 6, 9, 12, 0, 0, DateTimeKind.Utc);
        var handler = new UpdateProposalCommandHandler(context, new FixedDateTimeService(now));

        var result = await handler.Handle(
            new UpdateProposalCommand(proposal.ProposalsId, userId, new UpdateProposalRequest { CoverLetter = " Updated ", ProposedRate = 600m }),
            CancellationToken.None);

        Assert.True(result);
        Assert.Equal("Updated", proposal.CoverLetter);
        Assert.Equal(600m, proposal.ProposedRate);
        Assert.Equal(now, proposal.UpdatedAt);
    }

    [Fact]
    public async Task Handle_ThrowsNotFoundException_WhenProposalDoesNotBelongToFreelancer()
    {
        await using var context = TestDbContextFactory.Create();
        var ownerUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var ownerProfile = TestData.FreelancerProfile(ownerUserId);
        context.Users.Add(TestData.User(ownerUserId, UserRole.Freelancer));
        context.Users.Add(TestData.User(otherUserId, UserRole.Freelancer));
        context.FreelancerProfiles.Add(ownerProfile);
        context.FreelancerProfiles.Add(TestData.FreelancerProfile(otherUserId));
        var jobPost = TestData.JobPost(Guid.NewGuid());
        var proposal = TestData.Proposal(jobPost.JobPostsId, ownerProfile.FreelancerProfilesId);
        context.JobPosts.Add(jobPost);
        context.Proposals.Add(proposal);
        await context.SaveChangesAsync();
        var handler = new UpdateProposalCommandHandler(context, new FixedDateTimeService(DateTime.UtcNow));

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new UpdateProposalCommand(proposal.ProposalsId, otherUserId, new UpdateProposalRequest()),
            CancellationToken.None));
    }
}
