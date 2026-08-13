using Application.Features.JobPosts.Client.Common;
using Application.Common.Exceptions;
using Application.Features.JobPosts.Client.GetMyJobPostDetail.Queries;
using Domain.Entities;
using Domain.Enums.Contracts;
using Domain.Enums.ESign;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.JobPosts.Client;

public class GetMyJobPostDetailQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsFullDetailAndVisibleProposalCount()
    {
        var context = new InMemoryApplicationDbContext();
        var userId = Guid.NewGuid();
        var clientProfileId = Guid.NewGuid();
        var majorId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var majorCategoryId = Guid.NewGuid();
        var jobPostId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 6, 14, 9, 0, 0, DateTimeKind.Utc);
        var updatedAt = createdAt.AddHours(3);
        var endDate = createdAt.AddDays(14);

        var major = new Major
        {
            MajorsId = majorId,
            Name = "Creative",
            Slug = "creative",
            IsActive = true,
            CreatedAt = createdAt
        };

        var category = new Category
        {
            CategoriesId = categoryId,
            Name = "Design",
            Slug = "design",
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
            Name = "Figma",
            IsActive = true,
            CreatedAt = createdAt
        };

        var jobPost = CreateJobPost(clientProfileId, "Product redesign", createdAt);
        jobPost.JobPostsId = jobPostId;
        jobPost.Description = "Redesign a SaaS dashboard.";
        jobPost.MajorCategoryId = majorCategoryId;
        jobPost.MajorCategory = majorCategory;
        jobPost.BudgetMin = 1000m;
        jobPost.BudgetMax = 2500m;
        jobPost.Currency = "USD";
        jobPost.EstimatedDuration = "3 weeks";
        jobPost.Location = "Remote";
        jobPost.Status = 3;
        jobPost.Visibility = 2;
        jobPost.EndDate = endDate;
        jobPost.UpdatedAt = updatedAt;
        jobPost.CustomSkillNames = new[] { "Design systems" };
        jobPost.JobPostSkills.Add(new JobPostSkill
        {
            JobPostSkillsId = Guid.NewGuid(),
            JobPostsId = jobPostId,
            SkillsId = skillId,
            Skills = skill
        });
        jobPost.JobPostAttachments.Add(new JobPostAttachment
        {
            JobPostAttachmentsId = attachmentId,
            JobPostsId = jobPostId,
            FileName = "brief.pdf",
            FileUrl = "https://cdn.example/brief.pdf",
            CreatedAt = createdAt
        });
        jobPost.Proposals.Add(new Proposal { ProposalsId = Guid.NewGuid(), JobPostsId = jobPostId, Status = 0 });
        jobPost.Proposals.Add(new Proposal { ProposalsId = Guid.NewGuid(), JobPostsId = jobPostId, Status = 1 });
        jobPost.Proposals.Add(new Proposal { ProposalsId = Guid.NewGuid(), JobPostsId = jobPostId, Status = 5 });

        context.AddSet(new ClientProfile { ClientProfilesId = clientProfileId, UserId = userId });
        context.AddSet(jobPost);

        var handler = new GetMyJobPostDetailQueryHandler(context);

        var dto = await handler.Handle(
            new GetMyJobPostDetailQuery(userId, jobPostId),
            CancellationToken.None);

        Assert.Equal(jobPostId, dto.JobPostsId);
        Assert.Equal(clientProfileId, dto.ClientProfilesId);
        Assert.Equal("Product redesign", dto.Title);
        Assert.Equal("Redesign a SaaS dashboard.", dto.Description);
        Assert.Equal(majorCategoryId, dto.MajorCategoryId);
        Assert.Equal(majorId, dto.MajorId);
        Assert.Equal("Creative", dto.MajorName);
        Assert.Equal(categoryId, dto.CategoryId);
        Assert.Equal("Design", dto.CategoryName);
        Assert.Equal(1000m, dto.BudgetMin);
        Assert.Equal(2500m, dto.BudgetMax);
        Assert.Equal("USD", dto.Currency);
        Assert.Equal("3 weeks", dto.EstimatedDuration);
        Assert.Equal("Remote", dto.Location);
        Assert.Equal(2, dto.Visibility);
        Assert.Equal(3, dto.Status);
        Assert.Equal(endDate, dto.EndDate);
        Assert.Equal(createdAt, dto.CreatedAt);
        Assert.Equal(updatedAt, dto.UpdatedAt);
        Assert.Equal(2, dto.ProposalCount);
        Assert.Equal(new[] { "Design systems" }, dto.CustomSkillNames);

        var returnedSkill = Assert.Single(dto.Skills);
        Assert.Equal(skillId, returnedSkill.SkillsId);
        Assert.Equal("Figma", returnedSkill.SkillName);

        var returnedAttachment = Assert.Single(dto.Attachments);
        Assert.Equal(attachmentId, returnedAttachment.JobPostAttachmentsId);
        Assert.Equal("brief.pdf", returnedAttachment.FileName);
        Assert.Equal("https://cdn.example/brief.pdf", returnedAttachment.FileUrl);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    public async Task Handle_ReturnsOwnedJobRegardlessOfStatusOrVisibility(int status, int visibility)
    {
        var context = new InMemoryApplicationDbContext();
        var userId = Guid.NewGuid();
        var clientProfileId = Guid.NewGuid();
        var jobPost = CreateJobPost(clientProfileId, "Owned job", DateTime.UtcNow);
        jobPost.Status = status;
        jobPost.Visibility = visibility;

        context.AddSet(new ClientProfile { ClientProfilesId = clientProfileId, UserId = userId });
        context.AddSet(jobPost);

        var handler = new GetMyJobPostDetailQueryHandler(context);

        var dto = await handler.Handle(
            new GetMyJobPostDetailQuery(userId, jobPost.JobPostsId),
            CancellationToken.None);

        Assert.Equal(status, dto.Status);
        Assert.Equal(visibility, dto.Visibility);
    }

    [Fact]
    public async Task Handle_ReturnsProjectRequestSetupProgressForResumeFlow()
    {
        var context = new InMemoryApplicationDbContext();
        var userId = Guid.NewGuid();
        var clientProfileId = Guid.NewGuid();
        var jobPostId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 6, 20, 9, 0, 0, DateTimeKind.Utc);
        var jobPost = CreateJobPost(clientProfileId, "Needs milestones", createdAt);
        jobPost.JobPostsId = jobPostId;

        context.AddSet(new ClientProfile { ClientProfilesId = clientProfileId, UserId = userId });
        context.AddSet(jobPost);
        context.AddSet(new Contract
        {
            ContractsId = contractId,
            JobPostsId = jobPostId,
            ClientProfilesId = clientProfileId,
            Title = jobPost.Title,
            Description = jobPost.Description,
            TotalBudget = 100m,
            Status = (int)ContractStatus.PendingFreelancerSelection,
            CreatedAt = createdAt
        });
        context.AddSet(new EsignDocument
        {
            EsignDocumentsId = documentId,
            EsignTemplatesId = Guid.NewGuid(),
            JobPostsId = jobPostId,
            ContractsId = null,
            DocumentCode = $"DOC-{documentId:N}"[..32],
            RenderedHtmlContent = "<p>Job</p>",
            Status = (int)ESignDocumentStatus.FullySigned,
            CreatedAt = createdAt
        });
        context.AddSet<Milestone>();

        var handler = new GetMyJobPostDetailQueryHandler(context);

        var dto = await handler.Handle(
            new GetMyJobPostDetailQuery(userId, jobPostId),
            CancellationToken.None);

        Assert.NotNull(dto.SetupProgress);
        Assert.Equal(JobPostSetupStepNames.ReadyToPublish, dto.SetupProgress.NextIncompleteStep);
        Assert.True(dto.SetupProgress.IsDetailsComplete);
        Assert.Null(dto.SetupProgress.ContractId);
        Assert.Null(dto.SetupProgress.ESignDocumentId);
        Assert.Null(dto.SetupProgress.ESignStatus);
        Assert.False(dto.SetupProgress.HasMilestones);
        Assert.True(dto.SetupProgress.CanPublish);
    }

    [Fact]
    public async Task Handle_ThrowsWhenClientProfileDoesNotExist()
    {
        var context = new InMemoryApplicationDbContext();
        context.AddSet<ClientProfile>();
        context.AddSet<JobPost>();

        var handler = new GetMyJobPostDetailQueryHandler(context);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(
                new GetMyJobPostDetailQuery(Guid.NewGuid(), Guid.NewGuid()),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ThrowsWhenJobDoesNotExist()
    {
        var context = new InMemoryApplicationDbContext();
        var userId = Guid.NewGuid();
        var clientProfileId = Guid.NewGuid();
        context.AddSet(new ClientProfile { ClientProfilesId = clientProfileId, UserId = userId });
        context.AddSet<JobPost>();

        var handler = new GetMyJobPostDetailQueryHandler(context);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(
                new GetMyJobPostDetailQuery(userId, Guid.NewGuid()),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ThrowsWhenJobBelongsToAnotherClient()
    {
        var context = new InMemoryApplicationDbContext();
        var userId = Guid.NewGuid();
        var clientProfileId = Guid.NewGuid();
        var otherClientProfileId = Guid.NewGuid();
        var otherClientJob = CreateJobPost(otherClientProfileId, "Other client job", DateTime.UtcNow);

        context.AddSet(new ClientProfile { ClientProfilesId = clientProfileId, UserId = userId });
        context.AddSet(otherClientJob);

        var handler = new GetMyJobPostDetailQueryHandler(context);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(
                new GetMyJobPostDetailQuery(userId, otherClientJob.JobPostsId),
                CancellationToken.None));
    }

    private static JobPost CreateJobPost(Guid clientProfileId, string title, DateTime createdAt)
    {
        return new JobPost
        {
            JobPostsId = Guid.NewGuid(),
            ClientProfilesId = clientProfileId,
            Title = title,
            Description = $"{title} description",
            MajorCategoryId = Guid.NewGuid(),
            Status = 0,
            Visibility = 0,
            CreatedAt = createdAt
        };
    }
}
