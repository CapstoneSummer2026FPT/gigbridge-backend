namespace Domain.Enums.AiInterviews;

public enum AiInterviewDefinitionStatus
{
    AwaitingExternalCapability = 0,
    Active = 1,
    Closed = 2
}

public enum AiInterviewAttemptStatus
{
    InProgress = 0,
    Completed = 1,
    Failed = 2
}
