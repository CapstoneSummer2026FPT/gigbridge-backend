using Application.Common.Exceptions;
using Application.Features.Contracts.Freelancer.GetMyCompletedProjects.Queries;
using Domain.Entities;
using Domain.Enums;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Contracts.Freelancer;

public class GetMyCompletedProjectsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsCompletedProjectsWithFullJobPostInfo()
    {
        var fixture = CompletedProjectFixture.Create();
        var handler = new GetMyCompletedProjectsQueryHandler(fixture.Context);

        var result = await handler.Handle(
            new GetMyCompletedProjectsQuery(fixture.FreelancerUserId),
            CancellationToken.None);

        var project = Assert.Single(result);
        Assert.Equal(fixture.CompletedContractId, project.ContractId);
        Assert.Equal(fixture.CompletedJobPostId, project.JobPostsId);
        Assert.Equal(500m, project.TotalBudget);
        Assert.Equal((int)ContractStatus.Completed, project.Status);
        Assert.Equal(fixture.CompletedAt, project.CompletedAt);
        Assert.Equal("Client Name", project.ClientName);
        Assert.True(project.CanReview);
        Assert.False(project.HasReviewedByCurrentUser);

        var jobPost = project.JobPost;
        Assert.Equal(fixture.CompletedJobPostId, jobPost.JobPostsId);
        Assert.Equal(fixture.ClientProfileId, jobPost.ClientProfilesId);
        Assert.Equal("Completed project", jobPost.Title);
        Assert.Equal("Full project description.", jobPost.Description);
        Assert.Equal(1000m, jobPost.BudgetMin);
        Assert.Equal(2500m, jobPost.BudgetMax);
        Assert.Equal("GCOIN", jobPost.Currency);
        Assert.Equal("4 weeks", jobPost.EstimatedDuration);
        Assert.Equal(fixture.MajorCategoryId, jobPost.MajorCategoryId);
        Assert.Equal(fixture.MajorId, jobPost.MajorId);
        Assert.Equal("Creative", jobPost.MajorName);
        Assert.Equal(fixture.CategoryId, jobPost.CategoryId);
        Assert.Equal("Design", jobPost.CategoryName);
        Assert.Equal(1500, jobPost.EloPoints);
        Assert.Equal(new[] { "Design systems" }, jobPost.CustomSkillNames);
        Assert.True(jobPost.HasAiInterview);

        var skill = Assert.Single(jobPost.Skills);
        Assert.Equal(fixture.SkillId, skill.SkillsId);
        Assert.Equal("Figma", skill.SkillName);

        var attachment = Assert.Single(jobPost.Attachments);
        Assert.Equal("brief.pdf", attachment.FileName);
        Assert.Equal("https://cdn.example/brief.pdf", attachment.FileUrl);

        var plan = Assert.Single(jobPost.MilestonePlans);
        Assert.Equal("Milestone 1", plan.Title);
        Assert.Equal(2500m, plan.Amount);
        var workItem = Assert.Single(plan.WorkItems);
        Assert.Equal("Deliverable A", workItem.Title);
    }

    [Fact]
    public async Task Handle_ExcludesNonCompletedContracts()
    {
        var fixture = CompletedProjectFixture.Create();
        var handler = new GetMyCompletedProjectsQueryHandler(fixture.Context);

        var result = await handler.Handle(
            new GetMyCompletedProjectsQuery(fixture.FreelancerUserId),
            CancellationToken.None);

        var project = Assert.Single(result);
        Assert.Equal(fixture.CompletedContractId, project.ContractId);
        Assert.DoesNotContain(result, project => project.ContractId == fixture.ActiveContractId);
    }

    [Fact]
    public async Task Handle_ExcludesCompletedContractsOfOtherFreelancers()
    {
        var fixture = CompletedProjectFixture.Create();
        var handler = new GetMyCompletedProjectsQueryHandler(fixture.Context);

        var result = await handler.Handle(
            new GetMyCompletedProjectsQuery(fixture.FreelancerUserId),
            CancellationToken.None);

        var project = Assert.Single(result);
        Assert.Equal(fixture.CompletedContractId, project.ContractId);
        Assert.DoesNotContain(result, project => project.ContractId == fixture.OtherFreelancerContractId);
    }

    [Fact]
    public async Task Handle_ComputesReviewStateFromExistingReview()
    {
        var fixture = CompletedProjectFixture.Create();
        fixture.Context.Set<Review>().Add(new Review
        {
            ReviewsId = Guid.NewGuid(),
            ContractsId = fixture.CompletedContractId,
            ReviewerId = fixture.FreelancerUserId,
            RevieweeId = fixture.ClientUserId,
            Rating = 5m,
            CreatedAt = DateTime.UtcNow
        });

        var handler = new GetMyCompletedProjectsQueryHandler(fixture.Context);

        var result = await handler.Handle(
            new GetMyCompletedProjectsQuery(fixture.FreelancerUserId),
            CancellationToken.None);

        var project = Assert.Single(result);
        Assert.False(project.CanReview);
        Assert.True(project.HasReviewedByCurrentUser);
    }

    [Fact]
    public async Task Handle_ThrowsWhenUserHasNoFreelancerProfile()
    {
        var fixture = CompletedProjectFixture.Create();
        var handler = new GetMyCompletedProjectsQueryHandler(fixture.Context);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            handler.Handle(
                new GetMyCompletedProjectsQuery(fixture.ClientUserId),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ReturnsEmptyListWhenNoCompletedProjects()
    {
        var context = new InMemoryApplicationDbContext();
        var freelancerUserId = Guid.NewGuid();
        var freelancerProfileId = Guid.NewGuid();
        var jobPostId = Guid.NewGuid();
        var contractId = Guid.NewGuid();

        context.AddSet(new User
        {
            UserId = freelancerUserId,
            FullName = "Freelancer",
            Email = "freelancer@example.com",
            Role = (int)UserRole.Freelancer
        });
        context.AddSet(new FreelancerProfile
        {
            FreelancerProfilesId = freelancerProfileId,
            UserId = freelancerUserId
        });

        var jobPost = new JobPost
        {
            JobPostsId = jobPostId,
            ClientProfilesId = Guid.NewGuid(),
            Title = "Active job",
            Description = "Active body",
            Status = 1,
            CreatedAt = DateTime.UtcNow
        };
        context.AddSet(jobPost);
        context.AddSet(new Contract
        {
            ContractsId = contractId,
            JobPostsId = jobPostId,
            ClientProfilesId = Guid.NewGuid(),
            FreelancerProfilesId = freelancerProfileId,
            Title = "Active job",
            Description = "Active body",
            TotalBudget = 100m,
            Status = (int)ContractStatus.Active,
            CreatedAt = DateTime.UtcNow
        });

        var handler = new GetMyCompletedProjectsQueryHandler(context);

        var result = await handler.Handle(
            new GetMyCompletedProjectsQuery(freelancerUserId),
            CancellationToken.None);

        Assert.Empty(result);
    }

    private sealed class CompletedProjectFixture
    {
        public InMemoryApplicationDbContext Context { get; private init; } = null!;

        public Guid ClientUserId { get; private init; }

        public Guid FreelancerUserId { get; private init; }

        public Guid ClientProfileId { get; private init; }

        public Guid FreelancerProfileId { get; private init; }

        public Guid CompletedJobPostId { get; private init; }

        public Guid CompletedContractId { get; private init; }

        public Guid ActiveContractId { get; private init; }

        public Guid OtherFreelancerContractId { get; private init; }

        public Guid MajorId { get; private init; }

        public Guid CategoryId { get; private init; }

        public Guid MajorCategoryId { get; private init; }

        public Guid SkillId { get; private init; }

        public DateTime CompletedAt { get; private init; }

        public static CompletedProjectFixture Create()
        {
            var context = new InMemoryApplicationDbContext();
            var clientUserId = Guid.NewGuid();
            var freelancerUserId = Guid.NewGuid();
            var otherFreelancerUserId = Guid.NewGuid();
            var clientProfileId = Guid.NewGuid();
            var freelancerProfileId = Guid.NewGuid();
            var otherFreelancerProfileId = Guid.NewGuid();
            var completedJobPostId = Guid.NewGuid();
            var completedContractId = Guid.NewGuid();
            var activeJobPostId = Guid.NewGuid();
            var activeContractId = Guid.NewGuid();
            var otherJobPostId = Guid.NewGuid();
            var otherContractId = Guid.NewGuid();
            var majorId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var majorCategoryId = Guid.NewGuid();
            var skillId = Guid.NewGuid();
            var now = new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc);

            var clientUser = new User
            {
                UserId = clientUserId,
                FullName = "Client Name",
                Email = "client@example.com",
                Role = (int)UserRole.Client
            };
            clientUser.UserEloScore = new UserEloScore
            {
                UserEloScoresId = Guid.NewGuid(),
                UserId = clientUserId,
                CurrentPoints = 1500,
                LastActivityAt = now,
                CreatedAt = now
            };

            var clientProfile = new ClientProfile
            {
                ClientProfilesId = clientProfileId,
                UserId = clientUserId,
                User = clientUser
            };

            var freelancerUser = new User
            {
                UserId = freelancerUserId,
                FullName = "Freelancer",
                Email = "freelancer@example.com",
                Role = (int)UserRole.Freelancer
            };
            var freelancerProfile = new FreelancerProfile
            {
                FreelancerProfilesId = freelancerProfileId,
                UserId = freelancerUserId,
                User = freelancerUser
            };

            var otherFreelancerUser = new User
            {
                UserId = otherFreelancerUserId,
                FullName = "Other Freelancer",
                Email = "other@example.com",
                Role = (int)UserRole.Freelancer
            };
            var otherFreelancerProfile = new FreelancerProfile
            {
                FreelancerProfilesId = otherFreelancerProfileId,
                UserId = otherFreelancerUserId,
                User = otherFreelancerUser
            };

            var major = new Major
            {
                MajorsId = majorId,
                Name = "Creative",
                Slug = "creative",
                IsActive = true,
                CreatedAt = now
            };
            var category = new Category
            {
                CategoriesId = categoryId,
                Name = "Design",
                Slug = "design",
                IsActive = true,
                CreatedAt = now
            };
            var majorCategory = new MajorCategory
            {
                MajorCategoriesId = majorCategoryId,
                MajorId = majorId,
                Major = major,
                CategoryId = categoryId,
                Category = category,
                CreatedAt = now
            };

            var skill = new Skill
            {
                SkillsId = skillId,
                Name = "Figma",
                IsActive = true,
                CreatedAt = now
            };

            var completedJobPost = new JobPost
            {
                JobPostsId = completedJobPostId,
                ClientProfilesId = clientProfileId,
                ClientProfiles = clientProfile,
                Title = "Completed project",
                Description = "Full project description.",
                MajorCategoryId = majorCategoryId,
                MajorCategory = majorCategory,
                BudgetMin = 1000m,
                BudgetMax = 2500m,
                Currency = "GCOIN",
                EstimatedDuration = "4 weeks",
                Location = "Remote",
                Status = 1,
                Visibility = 0,
                CustomSkillNames = new[] { "Design systems" },
                CreatedAt = now
            };
            completedJobPost.JobPostSkills.Add(new JobPostSkill
            {
                JobPostSkillsId = Guid.NewGuid(),
                JobPostsId = completedJobPostId,
                SkillsId = skillId,
                Skills = skill
            });
            completedJobPost.JobPostAttachments.Add(new JobPostAttachment
            {
                JobPostAttachmentsId = Guid.NewGuid(),
                JobPostsId = completedJobPostId,
                FileName = "brief.pdf",
                FileUrl = "https://cdn.example/brief.pdf",
                CreatedAt = now
            });
            var plan = new JobPostMilestonePlan
            {
                JobPostMilestonePlanId = Guid.NewGuid(),
                JobPostsId = completedJobPostId,
                Title = "Milestone 1",
                Description = "First milestone",
                Amount = 2500m,
                OrderIndex = 1,
                CreatedAt = now
            };
            plan.WorkItems.Add(new JobPostWorkItem
            {
                JobPostWorkItemId = Guid.NewGuid(),
                JobPostMilestonePlanId = plan.JobPostMilestonePlanId,
                Title = "Deliverable A",
                OrderIndex = 1
            });
            completedJobPost.JobPostMilestonePlans.Add(plan);

            var completedContract = new Contract
            {
                ContractsId = completedContractId,
                JobPostsId = completedJobPostId,
                ClientProfilesId = clientProfileId,
                FreelancerProfilesId = freelancerProfileId,
                Title = completedJobPost.Title,
                Description = completedJobPost.Description,
                TotalBudget = 500m,
                Status = (int)ContractStatus.Completed,
                CompletedAt = now,
                CreatedAt = now,
                ClientProfiles = clientProfile,
                JobPosts = completedJobPost
            };

            var activeJobPost = new JobPost
            {
                JobPostsId = activeJobPostId,
                ClientProfilesId = clientProfileId,
                Title = "Active job",
                Description = "Active body",
                Status = 1,
                CreatedAt = now
            };
            var activeContract = new Contract
            {
                ContractsId = activeContractId,
                JobPostsId = activeJobPostId,
                ClientProfilesId = clientProfileId,
                FreelancerProfilesId = freelancerProfileId,
                Title = "Active job",
                Description = "Active body",
                TotalBudget = 100m,
                Status = (int)ContractStatus.Active,
                CreatedAt = now,
                ClientProfiles = clientProfile,
                JobPosts = activeJobPost
            };

            var otherJobPost = new JobPost
            {
                JobPostsId = otherJobPostId,
                ClientProfilesId = clientProfileId,
                Title = "Other freelancer project",
                Description = "Other body",
                Status = 1,
                CreatedAt = now
            };
            var otherContract = new Contract
            {
                ContractsId = otherContractId,
                JobPostsId = otherJobPostId,
                ClientProfilesId = clientProfileId,
                FreelancerProfilesId = otherFreelancerProfileId,
                Title = "Other freelancer project",
                Description = "Other body",
                TotalBudget = 300m,
                Status = (int)ContractStatus.Completed,
                CompletedAt = now.AddDays(-1),
                CreatedAt = now,
                ClientProfiles = clientProfile,
                JobPosts = otherJobPost
            };

            context.AddSet(clientUser, freelancerUser, otherFreelancerUser);
            context.AddSet(clientUser.UserEloScore);
            context.AddSet(clientProfile);
            context.AddSet(freelancerProfile);
            context.AddSet(otherFreelancerProfile);
            context.AddSet(major);
            context.AddSet(category);
            context.AddSet(majorCategory);
            context.AddSet(skill);
            context.AddSet(completedJobPost);
            context.AddSet(completedContract);
            context.AddSet(activeJobPost);
            context.AddSet(activeContract);
            context.AddSet(otherJobPost);
            context.AddSet(otherContract);
            context.AddSet(new AiInterviewDefinition
            {
                AiInterviewDefinitionsId = Guid.NewGuid(),
                JobPostId = completedJobPostId,
                ClientUserId = clientUserId,
                Status = AiInterviewDefinitionStatus.Active,
                CreatedAt = now
            });

            return new CompletedProjectFixture
            {
                Context = context,
                ClientUserId = clientUserId,
                FreelancerUserId = freelancerUserId,
                ClientProfileId = clientProfileId,
                FreelancerProfileId = freelancerProfileId,
                CompletedJobPostId = completedJobPostId,
                CompletedContractId = completedContractId,
                ActiveContractId = activeContractId,
                OtherFreelancerContractId = otherContractId,
                MajorId = majorId,
                CategoryId = categoryId,
                MajorCategoryId = majorCategoryId,
                SkillId = skillId,
                CompletedAt = now
            };
        }
    }
}
