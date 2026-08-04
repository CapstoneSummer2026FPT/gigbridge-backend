using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.JobPosts.Public.GetClientOpenJobPosts.Queries;
using Domain.Entities;
using Domain.Enums;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.JobPosts.Public;

public class GetClientOpenJobPostsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsOnlyClientsOpenPublicJobPostsWithFullSummary()
    {
        var fixture = ClientOpenJobPostsFixture.Create();
        var handler = new GetClientOpenJobPostsQueryHandler(fixture.Context, new FixedDateTimeService(fixture.Now));

        var result = (await handler.Handle(
            new GetClientOpenJobPostsQuery(fixture.ClientUserId),
            CancellationToken.None)).ToList();

        var post = Assert.Single(result);
        Assert.Equal(fixture.OpenJobPostId, post.JobPostsId);
        Assert.Equal("Open project", post.Title);
        Assert.StartsWith("Full project description.", post.DescriptionPreview);
        Assert.Equal(fixture.MajorCategoryId, post.MajorCategoryId);
        Assert.Equal(fixture.MajorId, post.MajorId);
        Assert.Equal("Creative", post.MajorName);
        Assert.Equal(fixture.CategoryId, post.CategoryId);
        Assert.Equal("Design", post.CategoryName);
        Assert.Equal(1000m, post.BudgetMin);
        Assert.Equal(2500m, post.BudgetMax);
        Assert.Equal(fixture.ClientProfileId, post.ClientProfilesId);
        Assert.Equal("Client Name", post.ClientFullName);
        Assert.Equal(1500, post.EloPoints);
        Assert.Equal(new[] { "Design systems" }, post.CustomSkillNames);
        Assert.True(post.HasAiInterview);

        var skill = Assert.Single(post.Skills);
        Assert.Equal(fixture.SkillId, skill.SkillsId);
        Assert.Equal("Figma", skill.SkillName);
        Assert.Equal(new[] { "Figma" }, post.SkillNames);
    }

    [Fact]
    public async Task Handle_ExcludesDraftClosedAndPrivateJobPostsOfTheClient()
    {
        var fixture = ClientOpenJobPostsFixture.Create();
        var handler = new GetClientOpenJobPostsQueryHandler(fixture.Context, new FixedDateTimeService(fixture.Now));

        var result = (await handler.Handle(
            new GetClientOpenJobPostsQuery(fixture.ClientUserId),
            CancellationToken.None)).ToList();

        Assert.Single(result);
        Assert.DoesNotContain(result, post => post.JobPostsId == fixture.DraftJobPostId);
        Assert.DoesNotContain(result, post => post.JobPostsId == fixture.ClosedJobPostId);
        Assert.DoesNotContain(result, post => post.JobPostsId == fixture.PrivateJobPostId);
    }

    [Fact]
    public async Task Handle_ExcludesOpenJobPostsOfOtherClients()
    {
        var fixture = ClientOpenJobPostsFixture.Create();
        var handler = new GetClientOpenJobPostsQueryHandler(fixture.Context, new FixedDateTimeService(fixture.Now));

        var result = (await handler.Handle(
            new GetClientOpenJobPostsQuery(fixture.ClientUserId),
            CancellationToken.None)).ToList();

        Assert.Single(result);
        Assert.DoesNotContain(result, post => post.JobPostsId == fixture.OtherOpenJobPostId);
    }

    [Fact]
    public async Task Handle_RespectsPageIndexAndPageSize()
    {
        var now = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);
        var context = new InMemoryApplicationDbContext();
        var clientUserId = Guid.NewGuid();
        var clientProfile = new ClientProfile
        {
            ClientProfilesId = Guid.NewGuid(),
            UserId = clientUserId,
            User = new User { UserId = clientUserId, FullName = "Client", Role = (int)UserRole.Client }
        };
        context.AddSet(clientProfile);

        var ids = Enumerable.Range(1, 3).Select(_ => Guid.NewGuid()).ToList();
        var jobPosts = ids.Select((id, index) => new JobPost
        {
            JobPostsId = id,
            ClientProfilesId = clientProfile.ClientProfilesId,
            ClientProfiles = clientProfile,
            Title = $"Job {index + 1}",
            Description = "Body",
            Status = 1,
            Visibility = 0,
            CreatedAt = now.AddMinutes(index)
        }).ToArray();
        context.AddSet(jobPosts);

        var handler = new GetClientOpenJobPostsQueryHandler(context, new FixedDateTimeService(now));

        // Newest first: ids[2], ids[1] on page 1 (page size 2), ids[0] on page 2.
        var page1 = (await handler.Handle(
            new GetClientOpenJobPostsQuery(clientUserId, 1, 2),
            CancellationToken.None)).ToList();
        Assert.Equal(2, page1.Count);
        Assert.Equal(ids[2], page1[0].JobPostsId);
        Assert.Equal(ids[1], page1[1].JobPostsId);

        var page2 = (await handler.Handle(
            new GetClientOpenJobPostsQuery(clientUserId, 2, 2),
            CancellationToken.None)).ToList();
        Assert.Single(page2);
        Assert.Equal(ids[0], page2[0].JobPostsId);
    }

    [Fact]
    public async Task Handle_ReturnsEmptyWhenClientHasNoOpenJobPosts()
    {
        var fixture = ClientOpenJobPostsFixture.Create();
        var handler = new GetClientOpenJobPostsQueryHandler(fixture.Context, new FixedDateTimeService(fixture.Now));

        var result = await handler.Handle(
            new GetClientOpenJobPostsQuery(fixture.EmptyClientUserId),
            CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_ReturnsEmptyWhenUserIdHasNoClientProfile()
    {
        var fixture = ClientOpenJobPostsFixture.Create();
        var handler = new GetClientOpenJobPostsQueryHandler(fixture.Context, new FixedDateTimeService(fixture.Now));

        var result = await handler.Handle(
            new GetClientOpenJobPostsQuery(Guid.NewGuid()),
            CancellationToken.None);

        Assert.Empty(result);
    }

    private sealed class ClientOpenJobPostsFixture
    {
        public InMemoryApplicationDbContext Context { get; private init; } = null!;

        public Guid ClientUserId { get; private init; }

        public Guid ClientProfileId { get; private init; }

        public Guid EmptyClientUserId { get; private init; }

        public Guid MajorId { get; private init; }

        public Guid CategoryId { get; private init; }

        public Guid MajorCategoryId { get; private init; }

        public Guid SkillId { get; private init; }

        public Guid OpenJobPostId { get; private init; }

        public Guid DraftJobPostId { get; private init; }

        public Guid ClosedJobPostId { get; private init; }

        public Guid PrivateJobPostId { get; private init; }

        public Guid OtherOpenJobPostId { get; private init; }

        public DateTime Now { get; private init; }

        public static ClientOpenJobPostsFixture Create()
        {
            var context = new InMemoryApplicationDbContext();
            var clientUserId = Guid.NewGuid();
            var otherClientUserId = Guid.NewGuid();
            var emptyClientUserId = Guid.NewGuid();
            var clientProfileId = Guid.NewGuid();
            var otherClientProfileId = Guid.NewGuid();
            var emptyClientProfileId = Guid.NewGuid();
            var majorId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var majorCategoryId = Guid.NewGuid();
            var skillId = Guid.NewGuid();
            var openJobPostId = Guid.NewGuid();
            var draftJobPostId = Guid.NewGuid();
            var closedJobPostId = Guid.NewGuid();
            var privateJobPostId = Guid.NewGuid();
            var otherOpenJobPostId = Guid.NewGuid();
            var now = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);

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

            var otherClientUser = new User
            {
                UserId = otherClientUserId,
                FullName = "Other Client",
                Email = "other@example.com",
                Role = (int)UserRole.Client
            };
            var otherClientProfile = new ClientProfile
            {
                ClientProfilesId = otherClientProfileId,
                UserId = otherClientUserId,
                User = otherClientUser
            };

            var emptyClientProfile = new ClientProfile
            {
                ClientProfilesId = emptyClientProfileId,
                UserId = emptyClientUserId,
                User = new User { UserId = emptyClientUserId, FullName = "Empty Client", Role = (int)UserRole.Client }
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

            var openJobPost = CreateJobPost(openJobPostId, clientProfile, "Open project",
                majorCategoryId, majorCategory, skill, now, status: 1, visibility: 0);
            var draftJobPost = CreateJobPost(draftJobPostId, clientProfile, "Draft project",
                majorCategoryId, majorCategory, skill, now, status: 0, visibility: 0);
            var closedJobPost = CreateJobPost(closedJobPostId, clientProfile, "Closed project",
                majorCategoryId, majorCategory, skill, now, status: 2, visibility: 0);
            var privateJobPost = CreateJobPost(privateJobPostId, clientProfile, "Private project",
                majorCategoryId, majorCategory, skill, now, status: 1, visibility: 1);
            var otherOpenJobPost = CreateJobPost(otherOpenJobPostId, otherClientProfile, "Other open project",
                majorCategoryId, majorCategory, skill, now, status: 1, visibility: 0);

            context.AddSet(clientUser, otherClientUser);
            context.AddSet(clientUser.UserEloScore);
            context.AddSet(clientProfile, otherClientProfile, emptyClientProfile);
            context.AddSet(major);
            context.AddSet(category);
            context.AddSet(majorCategory);
            context.AddSet(skill);
            context.AddSet(openJobPost, draftJobPost, closedJobPost, privateJobPost, otherOpenJobPost);
            context.AddSet(new AiInterviewDefinition
            {
                AiInterviewDefinitionsId = Guid.NewGuid(),
                JobPostId = openJobPostId,
                ClientUserId = clientUserId,
                Status = AiInterviewDefinitionStatus.Active,
                CreatedAt = now
            });

            return new ClientOpenJobPostsFixture
            {
                Context = context,
                ClientUserId = clientUserId,
                ClientProfileId = clientProfileId,
                EmptyClientUserId = emptyClientUserId,
                MajorId = majorId,
                CategoryId = categoryId,
                MajorCategoryId = majorCategoryId,
                SkillId = skillId,
                OpenJobPostId = openJobPostId,
                DraftJobPostId = draftJobPostId,
                ClosedJobPostId = closedJobPostId,
                PrivateJobPostId = privateJobPostId,
                OtherOpenJobPostId = otherOpenJobPostId,
                Now = now
            };
        }

        private static JobPost CreateJobPost(
            Guid jobPostId,
            ClientProfile clientProfile,
            string title,
            Guid majorCategoryId,
            MajorCategory majorCategory,
            Skill skill,
            DateTime now,
            int status,
            int? visibility)
        {
            var jobPost = new JobPost
            {
                JobPostsId = jobPostId,
                ClientProfilesId = clientProfile.ClientProfilesId,
                ClientProfiles = clientProfile,
                Title = title,
                Description = "Full project description. This is long enough to exercise the preview truncation behavior of the summary projection.",
                MajorCategoryId = majorCategoryId,
                MajorCategory = majorCategory,
                BudgetMin = 1000m,
                BudgetMax = 2500m,
                Currency = "GCOIN",
                EstimatedDuration = "4 weeks",
                Location = "Remote",
                Status = status,
                Visibility = visibility,
                CustomSkillNames = new[] { "Design systems" },
                CreatedAt = now
            };
            jobPost.JobPostSkills.Add(new JobPostSkill
            {
                JobPostSkillsId = Guid.NewGuid(),
                JobPostsId = jobPostId,
                SkillsId = skill.SkillsId,
                Skills = skill
            });
            return jobPost;
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
