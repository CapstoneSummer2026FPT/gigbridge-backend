namespace Infrastructure.ExternalServices.Ai;

public class AiServiceOptions
{
    public const string SectionName = "AiService";

    public string BaseUrl { get; set; } = null!;

    public string ApiKey { get; set; } = null!;
}
