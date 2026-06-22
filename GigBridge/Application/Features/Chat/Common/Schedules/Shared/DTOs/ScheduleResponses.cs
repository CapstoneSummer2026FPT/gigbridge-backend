using Application.Features.Chat.Common.Messages.Send.DTOs;

namespace Application.Features.Chat.Common.Schedules;

public record ScheduleMeetingResponse(
    int Provider,
    int Status,
    Guid OrganizerUserId,
    string? JoinUri,
    string? FailureCode,
    bool CanRetry);

public record ScheduleResponse(
    Guid ScheduleId, Guid ConversationId, Guid CreatedByUserId, string Title, string? Details,
    DateTime ScheduledAtUtc, string TimeZoneId, int Status, int EditCount, int RemainingEdits, int Version,
    Guid? CancelledByUserId, string? CancellationReason, DateTime CreatedAt, DateTime? UpdatedAt,
    DateTime? CancelledAt, DateTime CutoffUtc, DateTime GraceExpiresAtUtc, bool CanEdit, bool CanCancel,
    int AgreementStatus, DateTime? CounterProposalCreatedAtUtc, DateTime? CounterProposalEditExpiresAtUtc,
    bool CanAccept, bool CanReject, bool CanProposeTime, bool CanEditCounterProposal,
    ScheduleMeetingResponse? Meeting = null);

public record ScheduleEventResponse(
    int SchemaVersion, Guid ScheduleId, Guid ConversationId, Guid ScheduleMessageId, int EventType,
    int EventSequence, int Status, string Title, string? Details, DateTime ScheduledAtUtc, string TimeZoneId,
    Guid ActorId, string ActorName, Guid CreatedByUserId, int EditCount, int RemainingEdits, int Version,
    DateTime CreatedAt, string? CancellationReason, DateTime CutoffUtc, DateTime GraceExpiresAtUtc, bool CanEdit, bool CanCancel,
    int AgreementStatus = 0, DateTime? CounterProposalCreatedAtUtc = null,
    DateTime? CounterProposalEditExpiresAtUtc = null, bool CanAccept = false, bool CanReject = false,
    bool CanProposeTime = false, bool CanEditCounterProposal = false,
    ScheduleMeetingResponse? Meeting = null);

public record ScheduleMutationResult(ScheduleResponse Schedule, MessageResponse Message);

public record OngoingScheduleResponse(bool HasOngoingSchedule, Guid? ScheduleId, DateTime? ScheduledAtUtc);
