namespace Application.Features.Chat.Common.FinalOffers.Respond.DTOs;

public record RespondFinalOfferRequest(
    Guid NegotiationOfferId,
    FinalOfferResponse Response,
    string? Reason);
