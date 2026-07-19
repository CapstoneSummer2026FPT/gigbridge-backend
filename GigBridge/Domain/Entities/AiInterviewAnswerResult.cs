namespace Domain.Entities;

public sealed class AiInterviewAnswerResult
{
    public Guid AiInterviewAnswerResultsId { get; set; }
    public Guid AiInterviewAttemptId { get; set; }
    public int QuestionIndex { get; set; }
    public string? QuestionText { get; set; }
    public string? Transcript { get; set; }
    public int? Score { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public AiInterviewAttempt Attempt { get; set; } = null!;
}
