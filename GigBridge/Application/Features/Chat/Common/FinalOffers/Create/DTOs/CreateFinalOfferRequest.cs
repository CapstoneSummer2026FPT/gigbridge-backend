namespace Application.Features.Chat.Common.FinalOffers.Create.DTOs;

public record CreateFinalOfferRequest(
    Guid ConversationId,
    decimal FinalPrice,
    string? ScopeSummary,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? ClientNote);
