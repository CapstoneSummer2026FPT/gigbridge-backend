using Domain.Enums;

namespace Domain.Entities;

public sealed class AiInterviewAttempt
{
    public Guid AiInterviewAttemptsId { get; set; }
    public Guid AiInterviewDefinitionId { get; set; }
    public Guid FreelancerUserId { get; set; }
    public string ExternalSessionId { get; set; } = string.Empty;
    public AiInterviewAttemptStatus Status { get; set; }
    public int? OverallScore { get; set; }
    public int? CompatibilityScore { get; set; }
    public string? EvaluationSummary { get; set; }
    public string? TechnicalSkillsJson { get; set; }
    public string? SoftSkillsJson { get; set; }
    public bool? RecommendedHire { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public AiInterviewDefinition Definition { get; set; } = null!;
    public User FreelancerUser { get; set; } = null!;
    public ICollection<AiInterviewAnswerResult> Answers { get; set; } = new List<AiInterviewAnswerResult>();
}
