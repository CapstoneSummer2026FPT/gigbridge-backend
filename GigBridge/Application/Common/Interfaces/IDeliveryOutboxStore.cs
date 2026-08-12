using Domain.Entities;
using Domain.Enums.Chat;

namespace Application.Common.Interfaces;

public interface IDeliveryOutboxStore
{
    Task<IReadOnlyList<DeliveryOutboxLease>> ClaimDueAsync(
        DeliveryChannel channel,
        DateTime now,
        DateTime leaseExpiresAt,
        int batchSize,
        CancellationToken cancellationToken);

    Task<int> InsertScheduleStartDeliveriesAsync(
        IReadOnlyCollection<ScheduleStartDeliveryInsert> deliveries,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ScheduleStartBackfillSchedule>> LoadScheduleStartBackfillPageAsync(
        DateTime windowStartAt,
        DateTime? lastScheduledAtUtc,
        Guid? lastScheduleId,
        int pageSize,
        CancellationToken cancellationToken);
}

public readonly record struct DeliveryOutboxLease(
    Guid DeliveryOutboxId,
    Guid ClaimToken);

public sealed record ScheduleStartDeliveryInsert(
    DeliveryOutbox Delivery,
    DateTime ExpectedScheduledAtUtc);

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
