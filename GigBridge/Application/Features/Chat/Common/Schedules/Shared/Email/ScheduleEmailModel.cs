namespace Application.Features.Chat.Common.Schedules;

public sealed record ScheduleEmailModel(
    string RecipientName,
    string ActorName,
    bool IsActor,
    string Title,
    string FormattedTime,
    string? Details,
    string? CancellationReason,
    string ScheduleUrl,
    string? MeetingUrl = null);
