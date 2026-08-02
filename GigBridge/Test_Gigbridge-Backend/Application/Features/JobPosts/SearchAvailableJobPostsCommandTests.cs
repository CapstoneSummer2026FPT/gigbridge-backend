using Application.Features.JobPosts.Public.SearchAvailableJobPosts.Commands;
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

    private static JobPost Job(
        string title,
        string category,
        string[] customSkills,
        DateTime createdAt,
        bool aiGenerated)
    {
        return new JobPost
        {
            JobPostsId = Guid.NewGuid(),
            Title = title,
            Description = title,
            CreatedAt = createdAt,
            IsAigenerated = aiGenerated,
            CustomSkillNames = customSkills,
            MajorCategory = new MajorCategory
            {
                Category = new Category { Name = category, Slug = category.ToLowerInvariant() }
            }
        };
    }
}
