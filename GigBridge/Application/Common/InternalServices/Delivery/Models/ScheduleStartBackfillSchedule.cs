using Domain.Enums.Chat;

namespace Application.Common.InternalServices.Delivery.Models;

public sealed record ScheduleStartBackfillSchedule(
    Guid ScheduleId,
    Guid ConversationId,
    string Title,
    string? Details,
    DateTime ScheduledAtUtc,
    ScheduleAgreementStatus AgreementStatus,
    int Version,
    MeetingProvisioningStatus MeetingStatus,
    string? MeetingJoinUri);
