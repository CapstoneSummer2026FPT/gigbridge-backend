namespace Application.Features.JobInvitations.Common.DTOs;

public sealed class CreateJobInvitationRequest
{
    public Guid JobPostId { get; set; }

    public Guid FreelancerProfileId { get; set; }

    public string? Message { get; set; }

    public DateTime? ExpiresAt { get; set; }
}
