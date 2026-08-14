namespace Application.Common.InternalServices.Delivery.Models;

public readonly record struct DeliveryOutboxLease(
    Guid DeliveryOutboxId,
    Guid ClaimToken);
