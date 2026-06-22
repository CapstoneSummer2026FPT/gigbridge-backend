using Domain.Enums;

namespace Domain.Entities;

public class Schedule
{
    private string _timeZoneId = "Asia/Ho_Chi_Minh";

    public Guid ScheduleId { get; set; }
    public Guid ConversationId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTime ScheduledAtUtc { get; set; }
    public string TimeZoneId
    {
        get => _timeZoneId;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            try
            {
                TimeZoneInfo.FindSystemTimeZoneById(value);
            }
            catch (TimeZoneNotFoundException ex)
            {
                throw new ArgumentException($"Unknown time zone ID '{value}'.", nameof(value), ex);
            }
            catch (InvalidTimeZoneException ex)
            {
                throw new ArgumentException($"Invalid time zone data for '{value}'.", nameof(value), ex);
            }

            _timeZoneId = value;
        }
    }
    public ScheduleStatus Status { get; set; }
    public ScheduleAgreementStatus AgreementStatus { get; set; }
    public DateTime? CounterProposalCreatedAtUtc { get; set; }
    public int EditCount { get; set; }
    public int Version { get; set; } = 1;
    public Guid? CancelledByUserId { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    public ScheduleMeetingProvider MeetingProvider { get; set; }
    public MeetingProvisioningStatus MeetingStatus { get; set; }
    public int MeetingAttempt { get; set; }
    public string? MeetingSpaceName { get; set; }
    public string? MeetingJoinUri { get; set; }
    public string? MeetingFailureCode { get; set; }
    public DateTime? MeetingLastAttemptAt { get; set; }

    public virtual Conversation Conversation { get; set; } = null!;
    public virtual User CreatedByUser { get; set; } = null!;
    public virtual User? CancelledByUser { get; set; }
    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
    public virtual ICollection<GoogleMeetProvisioningJob> MeetProvisioningJobs { get; set; } = new List<GoogleMeetProvisioningJob>();
}
