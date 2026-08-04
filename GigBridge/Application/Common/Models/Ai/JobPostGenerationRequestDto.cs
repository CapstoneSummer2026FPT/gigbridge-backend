using System.Text.Json.Serialization;

namespace Application.Common.Models.Ai;

public class JobPostGenerationRequestDto
{
    [JsonPropertyName("client_prompt")]
    public string ClientPrompt { get; set; } = null!;
}

public class JobPostHiringPlanGenerationRequestDto
{
    [JsonPropertyName("client_prompt")]
    public string ClientPrompt { get; set; } = null!;

    [JsonPropertyName("title")]
    public string Title { get; set; } = null!;

    [JsonPropertyName("description")]
    public string Description { get; set; } = null!;
}
