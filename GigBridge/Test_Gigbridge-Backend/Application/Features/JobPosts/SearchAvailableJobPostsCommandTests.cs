using Application.Features.JobPosts.Public.SearchAvailableJobPosts.Commands;
using Application.Features.JobPosts.Freelancer.GetProfileMatchedJobPosts.Queries;
using Domain.Entities;

namespace Test_Gigbridge_backend.Application.Features.JobPosts;

public sealed class SearchAvailableJobPostsCommandTests
{
    private static readonly DateTime Now = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Open_and_visible_filter_excludes_expired_open_jobs()
    {
        var openNoDeadline = Job("No deadline", "Engineering", [], Now, true, status: 1, visibility: 0);
        var openFutureDeadline = Job("Future deadline", "Engineering", [], Now, true, status: 1, visibility: 0);
        openFutureDeadline.EndDate = Now.AddDays(1);
        var openExpired = Job("Expired", "Engineering", [], Now, true, status: 1, visibility: 0);
        openExpired.EndDate = Now.AddDays(-1);
        var closedExpired = Job("Closed", "Engineering", [], Now, true, status: 2, visibility: 0);
        closedExpired.EndDate = Now.AddDays(-1);

        var result = SearchAvailableJobPostsCommandHandler.ApplyOpenAndVisibleFilter(
            new[] { openNoDeadline, openFutureDeadline, openExpired, closedExpired }.AsQueryable(), Now).ToList();

        Assert.Equal(new[] { openNoDeadline, openFutureDeadline }, result);
    }

    [Fact]
    public void Browse_filters_are_applied_to_the_full_query()
    {
        var matching = Job("Frontend role", "Engineering", ["React"], Now.AddDays(-2), aiGenerated: true);
        var wrongCategory = Job("Brand role", "Design", ["React"], Now.AddDays(-2), aiGenerated: true);
        var tooOld = Job("Legacy frontend", "Engineering", ["React"], Now.AddDays(-60), aiGenerated: true);
        var notAi = Job("Frontend support", "Engineering", ["React"], Now.AddDays(-2), aiGenerated: false);
        var request = new SearchAvailableJobPostsCommand(
            Category: "Engineering", Skills: "react", WorkType: "fixed",
            PostedWithinDays: 30, AiOnly: true);

        var result = SearchAvailableJobPostsCommandHandler.ApplyBrowseFilters(
            new[] { matching, wrongCategory, tooOld, notAi }.AsQueryable(), request, Now).ToList();

        Assert.Single(result);
        Assert.Same(matching, result[0]);
    }

    [Fact]
    public void Multiple_skill_terms_must_all_match_official_or_custom_skills()
    {
        var matching = Job("Full stack", "Engineering", ["React", "PostgreSQL"], Now, true);
        var partial = Job("Frontend", "Engineering", ["React"], Now, true);
        var request = new SearchAvailableJobPostsCommand(Skills: "react, postgres");

        var result = SearchAvailableJobPostsCommandHandler.ApplyBrowseFilters(
            new[] { matching, partial }.AsQueryable(), request, Now).ToList();

        Assert.Equal(new[] { matching }, result);
    }

    [Fact]
    public void Profile_filter_matches_only_the_freelancer_major()
    {
        var profileMajorId = Guid.NewGuid();
        var otherMajorId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var sameMajorDifferentCategory = Job(
            "Same major", "Design", ["Figma"], Now, true, majorId: profileMajorId);
        var otherMajorWithSameSkill = Job(
            "Other major", "Engineering", ["React"], Now, true, majorId: otherMajorId);
        otherMajorWithSameSkill.JobPostSkills.Add(new JobPostSkill { SkillsId = skillId });

        var result = GetProfileMatchedJobPostsQueryHandler.ApplyMajorMatchFilter(
            new[] { sameMajorDifferentCategory, otherMajorWithSameSkill }.AsQueryable(),
            profileMajorId).ToList();

        Assert.Equal(new[] { sameMajorDifferentCategory }, result);
    }

    [Fact]
    public void Profile_filter_excludes_jobs_without_a_major_mapping()
    {
        var profileMajorId = Guid.NewGuid();
        var matching = Job("Matching", "Engineering", [], Now, true, majorId: profileMajorId);
        var missingMapping = Job("Missing mapping", "Engineering", [], Now, true);
        missingMapping.MajorCategory = null;

        var result = GetProfileMatchedJobPostsQueryHandler.ApplyMajorMatchFilter(
            new[] { matching, missingMapping }.AsQueryable(),
            profileMajorId).ToList();

        Assert.Equal(new[] { matching }, result);
    }

    [Fact]
    public void Profile_sorting_uses_featured_then_created_date_without_skill_weighting()
    {
        var olderFeatured = Job("Featured", "Engineering", [], Now.AddDays(-5), true);
        olderFeatured.IsFeatured = true;
        olderFeatured.FeaturedUntil = Now.AddDays(1);
        var newerWithoutSkills = Job("Newer", "Engineering", [], Now.AddDays(-1), true);
        var olderWithSkills = Job("Skilled", "Engineering", ["React", "SQL"], Now.AddDays(-2), true);

        var result = GetProfileMatchedJobPostsQueryHandler.ApplyMajorMatchSorting(
            new[] { newerWithoutSkills, olderWithSkills, olderFeatured }.AsQueryable(),
            "relevance",
            Now).ToList();

        Assert.Equal(new[] { olderFeatured, newerWithoutSkills, olderWithSkills }, result);
    }

    [Fact]
    public void Missing_profile_major_returns_no_matches()
    {
        var result = GetProfileMatchedJobPostsQueryHandler.ApplyMajorMatchFilter(
            new[] { Job("Any", "Engineering", [], Now, true) }.AsQueryable(),
            null).ToList();

        Assert.Empty(result);
    }

    [Fact]
    public void Profile_eligibility_keeps_only_open_public_or_legacy_jobs_not_already_applied_to()
    {
        var publicJob = Job("Public", "Engineering", [], Now, true, status: 1, visibility: 0);
        var legacyJob = Job("Legacy", "Engineering", [], Now, true, status: 1, visibility: null);
        var privateJob = Job("Private", "Engineering", [], Now, true, status: 1, visibility: 1);
        var inviteOnlyJob = Job("Invite", "Engineering", [], Now, true, status: 1, visibility: 2);
        var draftJob = Job("Draft", "Engineering", [], Now, true, status: 0, visibility: 0);
        var appliedJob = Job("Applied", "Engineering", [], Now, true, status: 1, visibility: 0);

        var result = GetProfileMatchedJobPostsQueryHandler.ApplyEligibilityFilter(
            new[] { publicJob, legacyJob, privateJob, inviteOnlyJob, draftJob, appliedJob }.AsQueryable(),
            new[] { appliedJob.JobPostsId }.AsQueryable()).ToList();

        Assert.Equal(new[] { publicJob, legacyJob }, result);
    }

    [Fact]
    public void Manual_filters_are_combined_with_profile_matching_using_and_semantics()
    {
        var majorId = Guid.NewGuid();
        var matching = Job("Recent React", "Engineering", ["React"], Now.AddDays(-2), true, majorId);
        var wrongManualSkill = Job("Recent Figma", "Engineering", ["Figma"], Now.AddDays(-2), true, majorId);
        var tooOld = Job("Old React", "Engineering", ["React"], Now.AddDays(-60), true, majorId);

        var profileMatches = GetProfileMatchedJobPostsQueryHandler.ApplyMajorMatchFilter(
            new[] { matching, wrongManualSkill, tooOld }.AsQueryable(),
            majorId);
        var result = SearchAvailableJobPostsCommandHandler.ApplyBrowseFilters(
            profileMatches,
            new SearchAvailableJobPostsCommand(Skills: "react", PostedWithinDays: 30),
            Now).ToList();

        Assert.Equal(new[] { matching }, result);
    }

    private static JobPost Job(
        string title,
        string category,
        string[] customSkills,
        DateTime createdAt,
        bool aiGenerated,
        Guid? majorId = null,
        int status = 1,
        int? visibility = 0)
    {
        return new JobPost
        {
            JobPostsId = Guid.NewGuid(),
            Title = title,
            Description = title,
            CreatedAt = createdAt,
            Status = status,
            Visibility = visibility,
            IsAigenerated = aiGenerated,
            MajorCategoryId = majorId.HasValue ? Guid.NewGuid() : null,
            CustomSkillNames = customSkills,
            MajorCategory = new MajorCategory
            {
                MajorId = majorId ?? Guid.Empty,
                Category = new Category { Name = category, Slug = category.ToLowerInvariant() }
            }
        };
    }
}
