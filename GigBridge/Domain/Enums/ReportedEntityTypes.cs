namespace Domain.Enums;

public static class ReportedEntityTypes
{
    public const string User = "User";
    public const string JobPost = "JobPost";
    public const string Review = "Review";

    public static readonly string[] All = [User, JobPost, Review];

    public static bool IsSupported(string? entityType)
    {
        return All.Any(type => string.Equals(type, entityType, StringComparison.OrdinalIgnoreCase));
    }

    public static string Normalize(string entityType)
    {
        if (!IsSupported(entityType))
        {
            throw new ArgumentException($"Unsupported entity type: {entityType}", nameof(entityType));
        }
        return All.First(type => string.Equals(type, entityType, StringComparison.OrdinalIgnoreCase));
    }
}
