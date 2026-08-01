using System.Text.Json.Serialization;

namespace Application.Common.Models.Ai;

public class JobPostGenerationResponseDto
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = null!;

    [JsonPropertyName("major_id")]
    public string MajorId { get; set; } = null!;

    [JsonPropertyName("category_id")]
    public string CategoryId { get; set; } = null!;

    [JsonPropertyName("system_skill_ids")]
    public List<string> SystemSkillIds { get; set; } = new();

    [JsonPropertyName("custom_skills")]
    public List<string> CustomSkills { get; set; } = new();

    [JsonPropertyName("description")]
    public string Description { get; set; } = null!;

    [JsonPropertyName("is_ai_generated")]
    public bool IsAiGenerated { get; set; }

    [JsonPropertyName("question_recruitment")]
    public List<string> QuestionRecruitment { get; set; } = new();

    [JsonPropertyName("budget_min")]
    public decimal? BudgetMin { get; set; }

    [JsonPropertyName("budget_max")]
    public decimal? BudgetMax { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    [JsonPropertyName("milestones")]
    public List<GeneratedMilestoneResponseDto> Milestones { get; set; } = new();
}

public class GeneratedMilestoneResponseDto
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = null!;

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("estimated_duration")]
    public string EstimatedDuration { get; set; } = null!;

    [JsonPropertyName("due_date")]
    public string DueDate { get; set; } = null!;

    [JsonPropertyName("description")]
    public string Description { get; set; } = null!;

    [JsonPropertyName("deliverables")]
    public string Deliverables { get; set; } = null!;

    [JsonPropertyName("acceptance_criteria")]
    public string AcceptanceCriteria { get; set; } = null!;
}
