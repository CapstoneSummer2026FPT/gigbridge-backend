namespace Application.Features.Proposals.Freelancer.Cheating.DTOs;

public class LogProposalCheatingEventRequest
{
    public int EventType { get; set; }

    public Guid? JobPostQuestionId { get; set; }

    public string? ClientEventId { get; set; }

    public DateTime? OccurredAt { get; set; }

    public Dictionary<string, string?>? Metadata { get; set; }
}
