namespace Application.Features.Chat.FinalOffers.Respond.DTOs;

public record RespondFinalOfferRequest(
    Guid NegotiationOfferId,
    FinalOfferResponse Response,
    string? Reason);
