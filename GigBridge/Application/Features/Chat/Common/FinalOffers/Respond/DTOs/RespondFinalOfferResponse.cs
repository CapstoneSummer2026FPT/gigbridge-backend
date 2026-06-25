namespace Application.Features.Chat.Common.FinalOffers.Respond.DTOs;

public sealed record RespondFinalOfferResponse(
    Guid? ContractId,
    int? ContractStatus,
    string? Message);
