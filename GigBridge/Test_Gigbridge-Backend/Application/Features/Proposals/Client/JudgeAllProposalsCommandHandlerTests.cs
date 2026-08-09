using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Common.Models.Ai;
using Application.Features.Proposals.Client.JudgeAllProposals;
using Domain.Entities;
using NSubstitute;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Proposals.Client;

public class JudgeAllProposalsCommandHandlerTests
{
    [Fact]
    public async Task Handle_WithUnjudgedProposals_EvaluatesBatchAndSaves()
    {
        // Arrange
        var fixture = CreateFixture(new[] { "Answer 1", "Answer 2" }, hasExistingJudging: false);
        var expected = new VettingEvaluationResponseDto { Score = 90, RecommendedHire = true };
        fixture.AiClient
            .AnalyzeVettingAsync(Arg.Any<AnalyzeVettingRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        // Act
        var result = await fixture.Handler.Handle(
            new JudgeAllProposalsCommand
            {
                JobPostId = fixture.JobPostId,
                UserId = fixture.ClientUserId,
                BatchSize = 10
            },
            CancellationToken.None);

        // Assert
        Assert.Equal(1, result.ProcessedCount);
        Assert.Equal(0, result.RemainingCount);
        Assert.True(result.IsCompleted);

        await fixture.AiClient.Received(1).AnalyzeVettingAsync(
            Arg.Is<AnalyzeVettingRequestDto>(request =>
                request.FreelancerId == fixture.FreelancerUserId.ToString() &&
                request.QaPairs.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithEmptyAnswers_CreatesEmptyJudgingWithoutCallingAi()
    {
        // Arrange
        var fixture = CreateFixture(new string?[] { null, "" }, hasExistingJudging: false);

        // Act
        var result = await fixture.Handler.Handle(
            new JudgeAllProposalsCommand
            {
                JobPostId = fixture.JobPostId,
                UserId = fixture.ClientUserId,
                BatchSize = 10
            },
            CancellationToken.None);

        // Assert
        Assert.Equal(1, result.ProcessedCount);
        Assert.Equal(0, result.RemainingCount);
        Assert.True(result.IsCompleted);

        await fixture.AiClient.DidNotReceive().AnalyzeVettingAsync(
            Arg.Any<AnalyzeVettingRequestDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenAllAlreadyJudged_ReturnsZeroProcessedCount()
    {
        // Arrange
        var fixture = CreateFixture(new[] { "Answer 1" }, hasExistingJudging: true);

        // Act
        var result = await fixture.Handler.Handle(
            new JudgeAllProposalsCommand
            {
                JobPostId = fixture.JobPostId,
                UserId = fixture.ClientUserId,
                BatchSize = 10
            },
            CancellationToken.None);

        // Assert
        Assert.Equal(0, result.ProcessedCount);
        Assert.Equal(0, result.RemainingCount);
        Assert.True(result.IsCompleted);

        await fixture.AiClient.DidNotReceive().AnalyzeVettingAsync(
            Arg.Any<AnalyzeVettingRequestDto>(),
            Arg.Any<CancellationToken>());
    }

    private static Fixture CreateFixture(string?[] answerTexts, bool hasExistingJudging)
    {
        var context = new InMemoryApplicationDbContext();
        var clientUserId = Guid.NewGuid();
        var clientProfileId = Guid.NewGuid();
        var freelancerUserId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var jobPostId = Guid.NewGuid();

        var clientProfile = new ClientProfile
        {
            ClientProfilesId = clientProfileId,
            UserId = clientUserId
        };
        var freelancerProfile = new FreelancerProfile
        {
            FreelancerProfilesId = Guid.NewGuid(),
            UserId = freelancerUserId
        };
        var jobPost = new JobPost
        {
            JobPostsId = jobPostId,
            ClientProfilesId = clientProfileId,
            ClientProfiles = clientProfile,
            Title = "Backend Architect",
            Description = "Build capability."
        };
        var proposal = new Proposal
        {
            ProposalsId = proposalId,
            JobPostsId = jobPostId,
            JobPosts = jobPost,
            FreelancerProfilesId = freelancerProfile.FreelancerProfilesId,
            FreelancerProfiles = freelancerProfile,
            Status = 2, // Submitted
            SubmittedAt = DateTime.UtcNow
        };

        if (hasExistingJudging)
        {
            var judging = new ProposalAiJudging
            {
                ProposalAiJudgingsId = Guid.NewGuid(),
                ProposalId = proposalId,
                Score = 80,
                Summary = "Great candidate.",
                RecommendedHire = true,
                EvaluatedAt = DateTime.UtcNow
            };
            proposal.ProposalAiJudging = judging;
            context.AddSet(judging);
        }

        context.AddSet(clientProfile);
        context.AddSet(jobPost);
        context.AddSet(proposal);

        if (answerTexts != null && answerTexts.Length > 0)
        {
            var answers = answerTexts.Select((answerText, index) =>
            {
                var question = new JobPostQuestion
                {
                    JobPostQuestionsId = Guid.NewGuid(),
                    JobPostsId = jobPostId,
                    JobPosts = jobPost,
                    QuestionText = $"Question {index + 1}",
                    OrderIndex = index
                };
                return new ProposalAnswer
                {
                    ProposalAnswersId = Guid.NewGuid(),
                    ProposalsId = proposalId,
                    Proposals = proposal,
                    JobPostQuestionsId = question.JobPostQuestionsId,
                    JobPostQuestions = question,
                    AnswerText = answerText!
                };
            }).ToArray();
            context.AddSet(answers);
        }

        var aiClient = Substitute.For<IAiServiceClient>();
        return new Fixture(
            new JudgeAllProposalsCommandHandler(context, aiClient),
            aiClient,
            clientUserId,
            freelancerUserId,
            proposalId,
            jobPostId);
    }

    private sealed record Fixture(
        JudgeAllProposalsCommandHandler Handler,
        IAiServiceClient AiClient,
        Guid ClientUserId,
        Guid FreelancerUserId,
        Guid ProposalId,
        Guid JobPostId);
}
