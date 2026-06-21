namespace Domain.Entities;

public class DeliveryOutbox
{
    public Guid DeliveryOutboxId { get; set; }
    public string DeliveryKey { get; set; } = string.Empty;
    public Guid ScheduleId { get; set; }
    public Guid RecipientUserId { get; set; }
    public int EventSequence { get; set; }
    public int Channel { get; set; }
    public string Payload { get; set; } = string.Empty;
    public int Status { get; set; }
    public int AttemptCount { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
}
