namespace Application.Features.JobInvitations.Common.DTOs;

public sealed class BulkJobInvitationSkipDto
{
    public Guid JobPostId { get; set; }

    public Guid FreelancerProfileId { get; set; }

    public string Reason { get; set; } = string.Empty;
}

public sealed class BulkJobInvitationResultDto
{
    public List<JobInvitationDto> Created { get; set; } = new();

    public List<BulkJobInvitationSkipDto> Skipped { get; set; } = new();
}
