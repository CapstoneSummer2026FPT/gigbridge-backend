using Application.Features.Chat.Common.Messages.Send.DTOs;

namespace Application.Features.Chat.Common.Schedules;

public record CreateScheduleRequest(Guid ConversationId, string Title, string? Details, DateTimeOffset ScheduledAt,
    string TimeZoneId = "Asia/Ho_Chi_Minh");
public record UpdateScheduleRequest(string Title, string? Details, DateTimeOffset ScheduledAt, int ExpectedVersion);
public record CancelScheduleRequest(string Reason, int ExpectedVersion);

public record ScheduleResponse(
    Guid ScheduleId, Guid ConversationId, Guid CreatedByUserId, string Title, string? Details,
    DateTime ScheduledAtUtc, string TimeZoneId, int Status, int EditCount, int RemainingEdits, int Version,
    Guid? CancelledByUserId, string? CancellationReason, DateTime CreatedAt, DateTime? UpdatedAt,
    DateTime? CancelledAt, DateTime CutoffUtc, DateTime GraceExpiresAtUtc, bool CanEdit, bool CanCancel);

public record ScheduleEventResponse(
    int SchemaVersion, Guid ScheduleId, Guid ConversationId, Guid ScheduleMessageId, int EventType,
    int EventSequence, int Status, string Title, string? Details, DateTime ScheduledAtUtc, string TimeZoneId,
    Guid ActorId, string ActorName, Guid CreatedByUserId, int EditCount, int RemainingEdits, int Version,
    string? CancellationReason, DateTime CutoffUtc, DateTime GraceExpiresAtUtc, bool CanEdit, bool CanCancel);

public record ScheduleMutationResult(ScheduleResponse Schedule, MessageResponse Message);
public record OngoingScheduleResponse(bool HasOngoingSchedule, Guid? ScheduleId, DateTime? ScheduledAtUtc);
