using Application.Features.JobPosts.Client.Common;
using Application.Features.JobPosts.Client.GetMyJobPosts.Queries;
using Domain.Entities;
using Domain.Enums;
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

    [Fact]
    public async Task Handle_ReturnsSetupProgressForResumeFlow()
    {
        var context = new InMemoryApplicationDbContext();
        var userId = Guid.NewGuid();
        var clientProfileId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 6, 20, 9, 0, 0, DateTimeKind.Utc);

        var detailsJob = CreateJobPost(clientProfileId, "Untitled Job Post", createdAt);
        detailsJob.Description = string.Empty;

        var esignJob = CreateJobPost(clientProfileId, "Needs e-sign", createdAt.AddMinutes(1));
        var pendingDocumentJob = CreateJobPost(clientProfileId, "Pending document", createdAt.AddMinutes(2));
        var milestonesJob = CreateJobPost(clientProfileId, "Needs milestones", createdAt.AddMinutes(3));
        var readyJob = CreateJobPost(clientProfileId, "Ready to publish", createdAt.AddMinutes(4));
        var publishedJob = CreateJobPost(clientProfileId, "Published job", createdAt.AddMinutes(5));
        publishedJob.Status = 1;

        var esignContractId = Guid.NewGuid();
        var pendingContractId = Guid.NewGuid();
        var milestonesContractId = Guid.NewGuid();
        var readyContractId = Guid.NewGuid();
        var pendingDocumentId = Guid.NewGuid();
        var signedDocumentId = Guid.NewGuid();
        var readyDocumentId = Guid.NewGuid();

        context.AddSet(new ClientProfile { ClientProfilesId = clientProfileId, UserId = userId });
        context.AddSet(detailsJob, esignJob, pendingDocumentJob, milestonesJob, readyJob, publishedJob);
        context.AddSet(
            CreateContract(esignContractId, esignJob),
            CreateContract(pendingContractId, pendingDocumentJob),
            CreateContract(milestonesContractId, milestonesJob),
            CreateContract(readyContractId, readyJob));
        context.AddSet(
            CreateDocument(pendingDocumentId, pendingDocumentJob.JobPostsId, ESignDocumentStatus.PendingSignatures, createdAt),
            CreateDocument(signedDocumentId, milestonesJob.JobPostsId, ESignDocumentStatus.FullySigned, createdAt),
            CreateDocument(readyDocumentId, readyJob.JobPostsId, ESignDocumentStatus.FullySigned, createdAt));
        context.AddSet(new Milestone
        {
            MilestonesId = Guid.NewGuid(),
            ContractsId = readyContractId,
            Title = "Build",
            Amount = 100m,
            Status = (int)MilestoneStatus.Pending,
            CreatedAt = createdAt
        });

        var handler = new GetMyJobPostsQueryHandler(context);

        var result = (await handler.Handle(
            new GetMyJobPostsQuery { UserId = userId, PageIndex = 1, PageSize = 10 },
            CancellationToken.None)).ToDictionary(jobPost => jobPost.JobPostsId);

        AssertProgress(result[detailsJob.JobPostsId].SetupProgress, JobPostSetupStepNames.Details, false, null, null, null, false, false);
        AssertProgress(result[esignJob.JobPostsId].SetupProgress, JobPostSetupStepNames.ESign, true, esignContractId, null, null, false, false);
        AssertProgress(result[pendingDocumentJob.JobPostsId].SetupProgress, JobPostSetupStepNames.ESign, true, pendingContractId, pendingDocumentId, (int)ESignDocumentStatus.PendingSignatures, false, false);
        AssertProgress(result[milestonesJob.JobPostsId].SetupProgress, JobPostSetupStepNames.Milestones, true, milestonesContractId, signedDocumentId, (int)ESignDocumentStatus.FullySigned, false, false);
        AssertProgress(result[readyJob.JobPostsId].SetupProgress, JobPostSetupStepNames.ReadyToPublish, true, readyContractId, readyDocumentId, (int)ESignDocumentStatus.FullySigned, true, true);
        AssertProgress(result[publishedJob.JobPostsId].SetupProgress, JobPostSetupStepNames.Published, true, null, null, null, false, false);
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

    private static Contract CreateContract(Guid contractId, JobPost jobPost)
    {
        return new Contract
        {
            ContractsId = contractId,
            JobPostsId = jobPost.JobPostsId,
            ClientProfilesId = jobPost.ClientProfilesId,
            Title = jobPost.Title,
            Description = jobPost.Description,
            TotalBudget = 100m,
            Status = (int)ContractStatus.PendingFreelancerSelection,
            CreatedAt = jobPost.CreatedAt
        };
    }

    private static EsignDocument CreateDocument(
        Guid documentId,
        Guid jobPostId,
        ESignDocumentStatus status,
        DateTime createdAt)
    {
        return new EsignDocument
        {
            EsignDocumentsId = documentId,
            EsignTemplatesId = Guid.NewGuid(),
            JobPostsId = jobPostId,
            ContractsId = null,
            DocumentCode = $"DOC-{documentId:N}"[..32],
            RenderedHtmlContent = "<p>Job</p>",
            Status = (int)status,
            CreatedAt = createdAt
        };
    }

    private static void AssertProgress(
        JobPostSetupProgressDto? progress,
        string nextStep,
        bool isDetailsComplete,
        Guid? contractId,
        Guid? documentId,
        int? documentStatus,
        bool hasMilestones,
        bool canPublish)
    {
        Assert.NotNull(progress);
        Assert.Equal(nextStep, progress.NextIncompleteStep);
        Assert.Equal(isDetailsComplete, progress.IsDetailsComplete);
        Assert.Equal(contractId, progress.ContractId);
        Assert.Equal(documentId, progress.ESignDocumentId);
        Assert.Equal(documentStatus, progress.ESignStatus);
        Assert.Equal(hasMilestones, progress.HasMilestones);
        Assert.Equal(canPublish, progress.CanPublish);
    }
}
