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

    [JsonPropertyName("budget_min")]
    public decimal? BudgetMin { get; set; }

    [JsonPropertyName("budget_max")]
    public decimal? BudgetMax { get; set; }

    [JsonPropertyName("estimated_duration")]
    public string? EstimatedDuration { get; set; }
}
