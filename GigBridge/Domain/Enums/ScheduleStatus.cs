namespace Domain.Enums;

public enum ScheduleStatus
{
    Scheduled = 0,
    Cancelled = 1,
    Completed = 2,
    Rejected = 3
}

public enum ScheduleAgreementStatus
{
    // Accepted is zero so schedules and metadata created before this workflow remain accepted.
    Accepted = 0,
    AwaitingFreelancer = 1,
    FreelancerRejectedAwaitingCounterproposal = 2,
    AwaitingClient = 3,
    ClientRejected = 4,
    AwaitingClientReschedule = 5,
    RescheduleRejected = 6
}

public enum ScheduleEventType
{
    Created = 0,
    Edited = 1,
    Cancelled = 2,
    Accepted = 3,
    Rejected = 4,
    CounterProposed = 5
}

public enum DeliveryOutboxStatus
{
    Pending = 0,
    Processing = 1,
    Delivered = 2,
    DeadLettered = 3,
    Cancelled = 4
}

public enum DeliveryChannel
{
    NotificationRealtime = 0,
    Email = 1
}
