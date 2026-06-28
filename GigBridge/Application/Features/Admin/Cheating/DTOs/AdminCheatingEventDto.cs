namespace Application.Features.Admin.Cheating.DTOs;

public class AdminCheatingEventDto
{
    public Guid ProposalCheatingEventId { get; init; }
    public Guid ProposalId { get; init; }
    public Guid FreelancerUserId { get; init; }
    public string FreelancerName { get; init; } = string.Empty;
    public string FreelancerEmail { get; init; } = string.Empty;
    public Guid JobPostId { get; init; }
    public string JobTitle { get; init; } = string.Empty;
    public Guid? JobPostQuestionId { get; init; }
    public int EventType { get; init; }
    public string ClientEventId { get; init; } = string.Empty;
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public string? Metadata { get; init; }
    public DateTime OccurredAt { get; init; }
    public DateTime CreatedAt { get; init; }
}
