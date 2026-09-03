namespace Application.Common.Options;

public sealed class AuthSessionOptions
{
    public const string SectionName = "AuthSessions";

    public int MaxActiveSessionsPerUser { get; set; } = 5;
}
