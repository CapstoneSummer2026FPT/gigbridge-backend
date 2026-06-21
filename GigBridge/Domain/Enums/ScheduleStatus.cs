namespace Domain.Enums;

public enum ScheduleStatus
{
    Scheduled = 0,
    Cancelled = 1
}

public enum ScheduleEventType
{
    Created = 0,
    Edited = 1,
    Cancelled = 2
}

public enum DeliveryOutboxStatus
{
    Pending = 0,
    Processing = 1,
    Delivered = 2,
    DeadLettered = 3
}

public enum DeliveryChannel
{
    NotificationRealtime = 0,
    Email = 1
}
