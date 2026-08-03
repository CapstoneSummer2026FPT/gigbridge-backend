namespace Application.Common.Options;

public sealed class DeliveryOutboxOptions
{
    public const string SectionName = "DeliveryOutbox";

    public int RealtimePollMilliseconds { get; set; } = 1_000;
    public int EmailPollMilliseconds { get; set; } = 5_000;
    public int BatchSize { get; set; } = 25;
    public int RealtimeMaxConcurrency { get; set; } = 8;
    public int EmailMaxConcurrency { get; set; } = 2;
    public int LeaseMinutes { get; set; } = 10;
    public int LeaseRecoveryIntervalSeconds { get; set; } = 60;
    public bool ScheduleStartBackfillEnabled { get; set; } = true;
    public int BackfillPageSize { get; set; } = 50;
    public int DeliveredPayloadRetentionDays { get; set; } = 30;
    public int RetentionIntervalMinutes { get; set; } = 60;
    public int RetentionBatchSize { get; set; } = 1_000;
}
