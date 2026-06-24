using Application.Features.JobPosts.Client.GetMyJobPosts.Queries;
using Domain.Entities;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.JobPosts.Client;

public class GetMyJobPostsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsFullJobPostFieldsAndVisibleProposalCount()
    {
        var context = new InMemoryApplicationDbContext();
        var userId = Guid.NewGuid();
        var clientProfileId = Guid.NewGuid();
        var majorId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var majorCategoryId = Guid.NewGuid();
        var jobPostId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 6, 14, 9, 0, 0, DateTimeKind.Utc);
        var updatedAt = createdAt.AddHours(2);
        var endDate = createdAt.AddDays(10);

        var major = new Major
        {
            MajorsId = majorId,
            Name = "Software",
            Slug = "software",
            IsActive = true,
            CreatedAt = createdAt
        };

        var category = new Category
        {
            CategoriesId = categoryId,
            Name = "Web Development",
            Slug = "web-development",
            IsActive = true,
            CreatedAt = createdAt
        };

        var majorCategory = new MajorCategory
        {
            MajorCategoriesId = majorCategoryId,
            MajorId = majorId,
            Major = major,
            CategoryId = categoryId,
            Category = category,
            CreatedAt = createdAt
        };

        var skill = new Skill
        {
            SkillsId = skillId,
            Name = "ASP.NET Core",
            IsActive = true,
            CreatedAt = createdAt
        };

        var jobPost = new JobPost
        {
            JobPostsId = jobPostId,
            ClientProfilesId = clientProfileId,
            Title = "Build booking flow",
            Description = "Create booking workflow and notification logic.",
            MajorCategoryId = majorCategoryId,
            MajorCategory = majorCategory,
            BudgetMin = 500m,
            BudgetMax = 1000m,
            Currency = "VND",
            EstimatedDuration = "2 weeks",
            Location = "Remote",
            Status = 1,
            Visibility = 2,
            EndDate = endDate,
            IsAigenerated = true,
            CustomSkillNames = new[] { "SignalR" },
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };

        jobPost.JobPostSkills.Add(new JobPostSkill
        {
            JobPostSkillsId = Guid.NewGuid(),
            JobPostsId = jobPostId,
            SkillsId = skillId,
            Skills = skill
        });
        jobPost.Proposals.Add(new Proposal { ProposalsId = Guid.NewGuid(), JobPostsId = jobPostId, Status = 0 });
        jobPost.Proposals.Add(new Proposal { ProposalsId = Guid.NewGuid(), JobPostsId = jobPostId, Status = 1 });
        jobPost.Proposals.Add(new Proposal { ProposalsId = Guid.NewGuid(), JobPostsId = jobPostId, Status = 3 });

        context.AddSet(new ClientProfile { ClientProfilesId = clientProfileId, UserId = userId });
        context.AddSet(jobPost);

        var handler = new GetMyJobPostsQueryHandler(context);

        var result = (await handler.Handle(
            new GetMyJobPostsQuery { UserId = userId, PageIndex = 1, PageSize = 10 },
            CancellationToken.None)).ToList();

        var dto = Assert.Single(result);
        Assert.Equal(jobPostId, dto.JobPostsId);
        Assert.Equal(clientProfileId, dto.ClientProfilesId);
        Assert.Equal("Build booking flow", dto.Title);
        Assert.Equal("Create booking workflow and notification logic.", dto.Description);
        Assert.Equal(majorCategoryId, dto.MajorCategoryId);
        Assert.Equal(majorId, dto.MajorId);
        Assert.Equal("Software", dto.MajorName);
        Assert.Equal(categoryId, dto.CategoryId);
        Assert.Equal("Web Development", dto.CategoryName);
        var returnedSkill = Assert.Single(dto.Skills);
        Assert.Equal(skillId, returnedSkill.SkillId);
        Assert.Equal("ASP.NET Core", returnedSkill.Name);
        Assert.Equal(new[] { "SignalR" }, dto.CustomSkillNames);
        Assert.Equal(500m, dto.BudgetMin);
        Assert.Equal(1000m, dto.BudgetMax);
        Assert.Equal("VND", dto.Currency);
        Assert.Equal("2 weeks", dto.EstimatedDuration);
        Assert.Equal("Remote", dto.Location);
        Assert.Equal(1, dto.Status);
        Assert.Equal(2, dto.Visibility);
        Assert.Equal(endDate, dto.EndDate);
        Assert.True(dto.IsAigenerated);
        Assert.Equal(createdAt, dto.CreatedAt);
        Assert.Equal(updatedAt, dto.UpdatedAt);
        Assert.Equal(2, dto.ProposalCount);
    }

    [Fact]
    public async Task Handle_FiltersByOwnerAndPreservesDescendingSortAndPaging()
    {
        var context = new InMemoryApplicationDbContext();
        var userId = Guid.NewGuid();
        var clientProfileId = Guid.NewGuid();
        var otherClientProfileId = Guid.NewGuid();
        var oldest = CreateJobPost(clientProfileId, "Oldest", new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc));
        var middle = CreateJobPost(clientProfileId, "Middle", new DateTime(2026, 6, 2, 9, 0, 0, DateTimeKind.Utc));
        var newest = CreateJobPost(clientProfileId, "Newest", new DateTime(2026, 6, 3, 9, 0, 0, DateTimeKind.Utc));
        var otherClientJob = CreateJobPost(otherClientProfileId, "Other client", new DateTime(2026, 6, 4, 9, 0, 0, DateTimeKind.Utc));

        context.AddSet(new ClientProfile { ClientProfilesId = clientProfileId, UserId = userId });
        context.AddSet(oldest, middle, newest, otherClientJob);

        var handler = new GetMyJobPostsQueryHandler(context);

        var result = (await handler.Handle(
            new GetMyJobPostsQuery { UserId = userId, PageIndex = 2, PageSize = 1 },
            CancellationToken.None)).ToList();

        var dto = Assert.Single(result);
        Assert.Equal(middle.JobPostsId, dto.JobPostsId);
        Assert.Equal("Middle", dto.Title);
    }

    private static JobPost CreateJobPost(Guid clientProfileId, string title, DateTime createdAt)
    {
        return new JobPost
        {
            JobPostsId = Guid.NewGuid(),
            ClientProfilesId = clientProfileId,
            Title = title,
            Description = $"{title} description",
            Status = 0,
            Visibility = 0,
            CreatedAt = createdAt
        };
    }
}
