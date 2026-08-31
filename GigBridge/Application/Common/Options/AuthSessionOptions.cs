namespace Application.Common.Options;

public sealed class AuthSessionOptions
{
    public const string SectionName = "AuthSessions";

    public bool Enabled { get; set; } = true;

    public int MaxActiveSessionsPerUser { get; set; } = 5;
}
