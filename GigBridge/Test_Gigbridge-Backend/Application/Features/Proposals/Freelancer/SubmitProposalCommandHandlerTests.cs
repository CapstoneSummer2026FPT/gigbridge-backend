using Application.Common.Exceptions;
using Application.Features.Proposals.Freelancer.SubmitProposal.Commands;
using Application.Features.Proposals.Freelancer.SubmitProposal.DTOs;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Test_Gigbridge_Backend.Support;

namespace Test_Gigbridge_Backend.Application.Features.Proposals.Freelancer;

public class SubmitProposalCommandHandlerTests
{
    private readonly DateTime _now = new(2026, 6, 9, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_ReturnsProposalId_WhenRequestIsValid()
    {
        await using var context = TestDbContextFactory.Create();
        var freelancerUserId = Guid.NewGuid();
        var freelancerProfile = TestData.FreelancerProfile(freelancerUserId);
        var jobPost = TestData.JobPost(Guid.NewGuid());
        context.Users.Add(TestData.User(freelancerUserId, UserRole.Freelancer));
        context.FreelancerProfiles.Add(freelancerProfile);
        context.JobPosts.Add(jobPost);
        await context.SaveChangesAsync();
        var handler = new SubmitProposalCommandHandler(context, new FixedDateTimeService(_now));

        var proposalId = await handler.Handle(
            new SubmitProposalCommand(CreateValidRequest(jobPost.JobPostsId), freelancerUserId),
            CancellationToken.None);

        var proposal = await context.Proposals.SingleAsync(x => x.ProposalsId == proposalId);
        Assert.Equal(0, proposal.Status);
        Assert.Equal(_now, proposal.SubmittedAt);
        Assert.Equal(freelancerProfile.FreelancerProfilesId, proposal.FreelancerProfilesId);
    }

    [Fact]
    public async Task Handle_ThrowsBadRequestException_WhenProposalAlreadyExists()
    {
        await using var context = TestDbContextFactory.Create();
        var freelancerUserId = Guid.NewGuid();
        var freelancerProfile = TestData.FreelancerProfile(freelancerUserId);
        var jobPost = TestData.JobPost(Guid.NewGuid());
        context.Users.Add(TestData.User(freelancerUserId, UserRole.Freelancer));
        context.FreelancerProfiles.Add(freelancerProfile);
        context.JobPosts.Add(jobPost);
        context.Proposals.Add(TestData.Proposal(jobPost.JobPostsId, freelancerProfile.FreelancerProfilesId));
        await context.SaveChangesAsync();
        var handler = new SubmitProposalCommandHandler(context, new FixedDateTimeService(_now));

        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new SubmitProposalCommand(CreateValidRequest(jobPost.JobPostsId), freelancerUserId),
            CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ThrowsBadRequestException_WhenJobPostIsClosed()
    {
        await using var context = TestDbContextFactory.Create();
        var freelancerUserId = Guid.NewGuid();
        context.Users.Add(TestData.User(freelancerUserId, UserRole.Freelancer));
        context.FreelancerProfiles.Add(TestData.FreelancerProfile(freelancerUserId));
        var jobPost = TestData.JobPost(Guid.NewGuid(), status: 2);
        context.JobPosts.Add(jobPost);
        await context.SaveChangesAsync();
        var handler = new SubmitProposalCommandHandler(context, new FixedDateTimeService(_now));

        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new SubmitProposalCommand(CreateValidRequest(jobPost.JobPostsId), freelancerUserId),
            CancellationToken.None));
    }

    private static SubmitProposalRequest CreateValidRequest(Guid jobPostId)
    {
        return new SubmitProposalRequest(
            JobPostsId: jobPostId,
            CoverLetter: "I can deliver this feature with clean architecture and clear communication throughout the project.",
            ProposedRate: 500m,
            ProposedDuration: "10 days");
    }
}
