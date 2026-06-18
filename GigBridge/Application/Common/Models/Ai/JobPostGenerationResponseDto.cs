using System.Text.Json.Serialization;

namespace Application.Common.Models.Ai;

public class JobPostGenerationResponseDto
{
    [JsonPropertyName("description")]
    public string Description { get; set; } = null!;

    [JsonPropertyName("is_ai_generated")]
    public bool IsAiGenerated { get; set; }
}
