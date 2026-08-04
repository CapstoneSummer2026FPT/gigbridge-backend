namespace Application.Features.JobInvitations.Common.DTOs;

public sealed class BulkCreateJobInvitationsRequest
{
    public List<Guid> JobPostIds { get; set; } = new();

    public List<Guid> FreelancerProfileIds { get; set; } = new();

    public string? Message { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public Guid? MatchRunId { get; set; }
}
