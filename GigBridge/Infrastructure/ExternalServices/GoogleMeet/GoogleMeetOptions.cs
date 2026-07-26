namespace Infrastructure.ExternalServices.GoogleMeet;

public class GoogleMeetOptions
{
    public const string SectionName = "GoogleMeet";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string AuthorizationEndpoint { get; set; } = "https://accounts.google.com/o/oauth2/v2/auth";
    public string TokenEndpoint { get; set; } = "https://oauth2.googleapis.com/token";
    public string RevocationEndpoint { get; set; } = "https://oauth2.googleapis.com/revoke";
    public string MeetApiBaseUrl { get; set; } = "https://meet.googleapis.com";
    public string BackendCallbackUri { get; set; } = string.Empty;
    public string FrontendCallbackUri { get; set; } = string.Empty;
}
