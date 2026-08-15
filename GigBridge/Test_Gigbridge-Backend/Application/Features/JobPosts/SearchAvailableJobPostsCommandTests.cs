using Application.Features.JobPosts.Public.SearchAvailableJobPosts.Commands;
using Application.Features.JobPosts.Freelancer.GetProfileMatchedJobPosts.Queries;
using Domain.Entities;

namespace Test_Gigbridge_backend.Application.Features.JobPosts;

public sealed class SearchAvailableJobPostsCommandTests
{
    private static readonly DateTime Now = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

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
    public void Profile_filter_matches_category_or_official_or_custom_skill()
    {
        var categoryId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var categoryMatch = Job("Category", "Engineering", [], Now, true, categoryId);
        var officialSkillMatch = Job("Official skill", "Design", [], Now, true);
        officialSkillMatch.JobPostSkills.Add(new JobPostSkill { SkillsId = skillId });
        var customSkillMatch = Job("Custom skill", "Design", ["REACT"], Now, true);
        var noMatch = Job("No match", "Design", ["Figma"], Now, true);

        var result = GetProfileMatchedJobPostsQueryHandler.ApplyProfileMatchFilter(
            new[] { categoryMatch, officialSkillMatch, customSkillMatch, noMatch }.AsQueryable(),
            [categoryId],
            [skillId],
            ["react"]).ToList();

        Assert.Equal(3, result.Count);
        Assert.DoesNotContain(noMatch, result);
    }

    [Fact]
    public void Profile_filter_supports_multiple_categories_with_or_semantics()
    {
        var firstCategoryId = Guid.NewGuid();
        var secondCategoryId = Guid.NewGuid();
        var first = Job("First", "Engineering", [], Now, true, firstCategoryId);
        var second = Job("Second", "Design", [], Now, true, secondCategoryId);
        var other = Job("Other", "Writing", [], Now, true, Guid.NewGuid());

        var result = GetProfileMatchedJobPostsQueryHandler.ApplyProfileMatchFilter(
            new[] { first, second, other }.AsQueryable(),
            [firstCategoryId, secondCategoryId],
            [],
            []).ToList();

        Assert.Equal(new[] { first, second }, result);
    }

    [Fact]
    public void Profile_relevance_prioritizes_both_dimensions_then_skill_overlap()
    {
        var categoryId = Guid.NewGuid();
        var firstSkillId = Guid.NewGuid();
        var secondSkillId = Guid.NewGuid();
        var categoryOnly = Job("Category only", "Engineering", [], Now, true, categoryId);
        var bothOneSkill = Job("Both one", "Engineering", [], Now, true, categoryId);
        bothOneSkill.JobPostSkills.Add(new JobPostSkill { SkillsId = firstSkillId });
        var bothTwoSkills = Job("Both two", "Engineering", [], Now, true, categoryId);
        bothTwoSkills.JobPostSkills.Add(new JobPostSkill { SkillsId = firstSkillId });
        bothTwoSkills.JobPostSkills.Add(new JobPostSkill { SkillsId = secondSkillId });

        var result = GetProfileMatchedJobPostsQueryHandler.ApplyProfileMatchSorting(
            new[] { categoryOnly, bothOneSkill, bothTwoSkills }.AsQueryable(),
            [categoryId],
            [firstSkillId, secondSkillId],
            [],
            "relevance",
            Now).ToList();

        Assert.Equal(new[] { bothTwoSkills, bothOneSkill, categoryOnly }, result);
    }

    [Fact]
    public void Empty_profile_criteria_returns_no_matches()
    {
        var result = GetProfileMatchedJobPostsQueryHandler.ApplyProfileMatchFilter(
            new[] { Job("Any", "Engineering", [], Now, true) }.AsQueryable(),
            [],
            [],
            []).ToList();

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
        var categoryId = Guid.NewGuid();
        var matching = Job("Recent React", "Engineering", ["React"], Now.AddDays(-2), true, categoryId);
        var wrongManualSkill = Job("Recent Figma", "Engineering", ["Figma"], Now.AddDays(-2), true, categoryId);
        var tooOld = Job("Old React", "Engineering", ["React"], Now.AddDays(-60), true, categoryId);

        var profileMatches = GetProfileMatchedJobPostsQueryHandler.ApplyProfileMatchFilter(
            new[] { matching, wrongManualSkill, tooOld }.AsQueryable(),
            [categoryId],
            [],
            []);
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
        Guid? majorCategoryId = null,
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
            MajorCategoryId = majorCategoryId,
            CustomSkillNames = customSkills,
            MajorCategory = new MajorCategory
            {
                Category = new Category { Name = category, Slug = category.ToLowerInvariant() }
            }
        };
    }
}
