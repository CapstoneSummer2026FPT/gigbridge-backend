namespace Domain.Entities;

public class DeliveryOutbox
{
    public Guid DeliveryOutboxId { get; set; }
    public string DeliveryKey { get; set; } = string.Empty;
    public Guid? ScheduleId { get; set; }

    /// <summary>
    /// Discriminates the payload shape when ScheduleId is null (see Domain.Enums.Delivery.DeliveryOutboxType).
    /// Null is treated as FinalContractEmail for backward compatibility with rows created before this column existed.
    /// </summary>
    public int? DeliveryType { get; set; }
    public Guid RecipientUserId { get; set; }
    public int EventSequence { get; set; }
    public int Channel { get; set; }
    public string Payload { get; set; } = string.Empty;
    public int Status { get; set; }
    public int AttemptCount { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public Guid? ClaimToken { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
}
