namespace Application.Common.InternalServices.Chat.Models;
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
