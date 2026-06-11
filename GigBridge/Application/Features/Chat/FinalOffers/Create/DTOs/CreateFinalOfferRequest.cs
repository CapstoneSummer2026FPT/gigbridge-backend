namespace Application.Features.Chat.FinalOffers.Create.DTOs;

public record CreateFinalOfferRequest(
    Guid ConversationId,
    decimal FinalPrice,
    string? ScopeSummary,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? ClientNote);
