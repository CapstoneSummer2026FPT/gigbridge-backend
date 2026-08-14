using Domain.Entities;

namespace Application.Common.InternalServices.Delivery.Models;

public sealed record ScheduleStartDeliveryInsert(
    DeliveryOutbox Delivery,
    DateTime ExpectedScheduledAtUtc);
