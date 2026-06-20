using System.Text.Json.Serialization;

namespace Application.Common.Models.Ai;

public class ClientQuestionDto
{
    [JsonPropertyName("question")]
    public string Question { get; set; } = null!;
}

public class MajorOptionDto
{
    [JsonPropertyName("major_id")]
    public string MajorId { get; set; } = null!;

    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;
}

public class CategoryOptionDto
{
    [JsonPropertyName("category_id")]
    public string CategoryId { get; set; } = null!;

    [JsonPropertyName("major_id")]
    public string MajorId { get; set; } = null!;

    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;
}

public class SkillOptionDto
{
    [JsonPropertyName("skill_id")]
    public string SkillId { get; set; } = null!;

    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;
}

public class JobPostGenerationRequestDto
{
    [JsonPropertyName("client_questions")]
    public List<ClientQuestionDto> ClientQuestions { get; set; } = new();

    [JsonPropertyName("allowed_majors")]
    public List<MajorOptionDto> AllowedMajors { get; set; } = new();

    [JsonPropertyName("allowed_categories")]
    public List<CategoryOptionDto> AllowedCategories { get; set; } = new();

    [JsonPropertyName("available_skills")]
    public List<SkillOptionDto> AvailableSkills { get; set; } = new();
}
