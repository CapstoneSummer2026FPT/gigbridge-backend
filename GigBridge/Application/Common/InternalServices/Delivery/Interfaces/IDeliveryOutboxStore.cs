using Application.Common.InternalServices.Delivery.Models;
using Domain.Enums.Chat;

namespace Application.Common.InternalServices.Delivery.Interfaces;

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
