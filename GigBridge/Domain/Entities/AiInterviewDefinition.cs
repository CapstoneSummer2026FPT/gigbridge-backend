using Domain.Enums;

namespace Domain.Entities;

public sealed class AiInterviewDefinition
{
    public Guid AiInterviewDefinitionsId { get; set; }
    public Guid JobPostId { get; set; }
    public Guid ClientUserId { get; set; }
    public string Language { get; set; } = "auto";
    public string Mode { get; set; } = "voice";
    public int QuestionCount { get; set; }
    public AiInterviewDefinitionStatus Status { get; set; }
    public string? ExternalReference { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public JobPost JobPost { get; set; } = null!;
    public User ClientUser { get; set; } = null!;
    public ICollection<AiInterviewAttempt> Attempts { get; set; } = new List<AiInterviewAttempt>();
}
