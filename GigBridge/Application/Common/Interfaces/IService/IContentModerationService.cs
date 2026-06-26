namespace Application.Common.Interfaces.IService;

public interface IContentModerationService
{
    ContentModerationResult ValidateJobPostContent(string? title, string? description);
}

public static class ContentModerationMessages
{
    public const string JobPostContentViolation =
        "Job post content violates community and legal safety standards. Please remove illegal, unsafe, fraudulent, adult, gambling, drug-related, or suspicious recruitment content before publishing.";
}

public class ContentModerationResult
{
    public bool IsAllowed { get; set; }

    public int RiskScore { get; set; }

    public List<string> Violations { get; set; } = new();

    public List<string> MatchedCategories { get; set; } = new();
}
