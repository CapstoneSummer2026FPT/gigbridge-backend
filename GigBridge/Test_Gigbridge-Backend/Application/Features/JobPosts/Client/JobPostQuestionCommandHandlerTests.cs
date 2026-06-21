using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Features.JobPosts.Client.Questions.CreateBulkJobPostQuestions.Commands;
using Application.Features.JobPosts.Client.Questions.CreateBulkJobPostQuestions.DTOs;
using Application.Features.JobPosts.Client.Questions.CreateJobPostQuestion.Commands;
using Application.Features.JobPosts.Client.Questions.CreateJobPostQuestion.DTOs;
using Domain.Entities;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.JobPosts.Client;

public class JobPostQuestionCommandHandlerTests
{
    [Fact]
    public async Task CreateQuestion_RejectsDuplicateOrderIndex()
    {
        var fixture = new JobPostQuestionFixture();
        fixture.Questions.Add(fixture.CreateQuestion(orderIndex: 0));
        var handler = new CreateJobPostQuestionCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now));

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(
                new CreateJobPostQuestionCommand(
                    fixture.JobPostId,
                    fixture.ClientUserId,
                    new CreateJobPostQuestionRequest
                    {
                        QuestionText = "Describe your experience.",
                        OrderIndex = 0,
                        IsRequired = true
                    }),
                CancellationToken.None));

        Assert.Single(fixture.Questions.Entities);
        Assert.Equal(0, fixture.Context.SaveChangesCount);
    }

    [Fact]
    public async Task CreateBulkQuestions_RejectsExistingOrderIndex()
    {
        var fixture = new JobPostQuestionFixture();
        fixture.Questions.Add(fixture.CreateQuestion(orderIndex: 1));
        var handler = new CreateBulkJobPostQuestionsCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now));

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(
                new CreateBulkJobPostQuestionsCommand(
                    fixture.JobPostId,
                    fixture.ClientUserId,
                    new CreateBulkJobPostQuestionsRequest
                    {
                        Questions = new List<CreateBulkJobPostQuestionItemRequest>
                        {
                            new()
                            {
                                QuestionText = "What is your timeline?",
                                OrderIndex = 0,
                                IsRequired = true
                            },
                            new()
                            {
                                QuestionText = "Share similar work.",
                                OrderIndex = 1,
                                IsRequired = true
                            }
                        }
                    }),
                CancellationToken.None));

        Assert.Single(fixture.Questions.Entities);
        Assert.Equal(0, fixture.Context.SaveChangesCount);
    }

    [Fact]
    public async Task CreateBulkQuestions_AddsQuestionsWhenOrderIndexesAreAvailable()
    {
        var fixture = new JobPostQuestionFixture();
        var handler = new CreateBulkJobPostQuestionsCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now));

        var result = await handler.Handle(
            new CreateBulkJobPostQuestionsCommand(
                fixture.JobPostId,
                fixture.ClientUserId,
                new CreateBulkJobPostQuestionsRequest
                {
                    Questions = new List<CreateBulkJobPostQuestionItemRequest>
                    {
                        new()
                        {
                            QuestionText = "What is your timeline?",
                            OrderIndex = 0,
                            IsRequired = true
                        },
                        new()
                        {
                            QuestionText = "Share similar work.",
                            OrderIndex = 1,
                            IsRequired = false
                        }
                    }
                }),
            CancellationToken.None);

        Assert.Equal(2, fixture.Questions.Entities.Count);
        Assert.Equal(2, result.Count());
        Assert.Equal(1, fixture.Context.SaveChangesCount);
    }

    private sealed class JobPostQuestionFixture
    {
        public JobPostQuestionFixture()
        {
            Context.AddSet(new ClientProfile
            {
                ClientProfilesId = ClientProfileId,
                UserId = ClientUserId
            });

            Context.AddSet(new JobPost
            {
                JobPostsId = JobPostId,
                ClientProfilesId = ClientProfileId,
                Title = "Build a website",
                Description = "Build a responsive website",
                Status = 0,
                CreatedAt = Now
            });

            Questions = Context.AddSet<JobPostQuestion>();
        }

        public InMemoryApplicationDbContext Context { get; } = new();

        public DateTime Now { get; } = new(2026, 6, 19, 8, 0, 0, DateTimeKind.Utc);

        public Guid ClientUserId { get; } = Guid.NewGuid();

        public Guid ClientProfileId { get; } = Guid.NewGuid();

        public Guid JobPostId { get; } = Guid.NewGuid();

        public TestDbSet<JobPostQuestion> Questions { get; }

        public JobPostQuestion CreateQuestion(int orderIndex)
        {
            return new JobPostQuestion
            {
                JobPostQuestionsId = Guid.NewGuid(),
                JobPostsId = JobPostId,
                QuestionText = "Existing question",
                OrderIndex = orderIndex,
                IsRequired = true,
                CreatedAt = Now
            };
        }
    }

    private sealed class FixedDateTimeService : IDateTimeService
    {
        public FixedDateTimeService(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; }
    }
}
