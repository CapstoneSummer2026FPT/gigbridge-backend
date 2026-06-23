using Domain.Enums;

namespace Domain.Entities;

public class GoogleMeetProvisioningJob
{
    public Guid GoogleMeetProvisioningJobId { get; set; }
    public Guid ScheduleId { get; set; }
    public Guid OrganizerUserId { get; set; }
    public int Attempt { get; set; }
    public GoogleMeetProvisioningJobStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? LeaseExpiresAt { get; set; }
    public string? FailureCode { get; set; }
    public string? ReturnedSpaceName { get; set; }
    public string? ReturnedJoinUri { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public virtual Schedule Schedule { get; set; } = null!;
    public virtual User OrganizerUser { get; set; } = null!;
}
