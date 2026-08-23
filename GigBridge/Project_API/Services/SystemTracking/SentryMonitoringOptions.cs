namespace Project_API.Services.SystemTracking;

public sealed class SentryMonitoringOptions
{
    public const string SectionName = "SentryMonitoring";

    public string BaseUrl { get; set; } = "https://sentry.io/api/0/";
    public string Organization { get; set; } = string.Empty;
    public string AuthToken { get; set; } = string.Empty;
    public string Environment { get; set; } = "production";
    public string StatsPeriod { get; set; } = "14d";
    public int CacheSeconds { get; set; } = 30;
    public string[] ProjectIds { get; set; } = [];

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Organization) &&
        !string.IsNullOrWhiteSpace(AuthToken);
}
