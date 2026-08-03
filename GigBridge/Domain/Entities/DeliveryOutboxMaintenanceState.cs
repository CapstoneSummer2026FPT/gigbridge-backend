namespace Domain.Entities;

public sealed class DeliveryOutboxMaintenanceState
{
    public string Operation { get; set; } = string.Empty;
    public DateTime WindowStartAt { get; set; }
    public DateTime? LastScheduledAtUtc { get; set; }
    public Guid? LastScheduleId { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
