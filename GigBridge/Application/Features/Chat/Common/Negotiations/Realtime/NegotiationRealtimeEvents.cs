namespace Application.Features.Chat.Common.Negotiations.Realtime;

public static class NegotiationRealtimeEvents
{
    public const string MilestonePlanUpdated = "NegotiationMilestonePlanUpdated";
    public const string FinalOfferCreated = "FinalOfferCreated";
    public const string FinalOfferResponded = "FinalOfferResponded";
    public const string ContractDraftUpdated = "ContractDraftUpdated";
}

public sealed record NegotiationMilestonePlanUpdatedPayload(
    Guid ConversationId,
    Guid UpdatedByUserId,
    DateTime UpdatedAt);
