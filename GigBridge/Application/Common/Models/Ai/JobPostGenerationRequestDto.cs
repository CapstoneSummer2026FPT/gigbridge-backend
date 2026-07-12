using System.Text.Json.Serialization;

namespace Application.Common.Models.Ai;

public class JobPostGenerationRequestDto
{
    [JsonPropertyName("client_prompt")]
    public string ClientPrompt { get; set; } = null!;
}
