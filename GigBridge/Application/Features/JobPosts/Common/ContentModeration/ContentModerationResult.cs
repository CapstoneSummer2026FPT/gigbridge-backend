namespace Application.Features.JobPosts.Common.ContentModeration;

public class ContentModerationResult
{
    public bool IsAllowed { get; set; }

    public int RiskScore { get; set; }

    public List<string> Violations { get; set; } = new();

    public List<string> MatchedCategories { get; set; } = new();
}
