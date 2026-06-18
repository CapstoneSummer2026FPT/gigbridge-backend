using System.Text.Json.Serialization;

namespace Application.Common.Models.Ai;

public class QuestionAnswerPairDto
{
    [JsonPropertyName("question")]
    public string Question { get; set; } = null!;

    [JsonPropertyName("answer")]
    public string Answer { get; set; } = null!;
}

public class JobPostGenerationRequestDto
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = null!;

    [JsonPropertyName("category")]
    public string Category { get; set; } = null!;

    [JsonPropertyName("skills")]
    public List<string> Skills { get; set; } = new();

    [JsonPropertyName("client_questions_and_freelancer_answers")]
    public List<QuestionAnswerPairDto> ClientQuestionsAndFreelancerAnswers { get; set; } = new();
}
