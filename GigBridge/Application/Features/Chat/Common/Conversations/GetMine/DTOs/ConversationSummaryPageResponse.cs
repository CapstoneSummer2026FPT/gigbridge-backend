namespace Application.Features.Chat.Common.Conversations.GetMine.DTOs;

public sealed record ConversationSummaryPageResponse(
    IReadOnlyList<ConversationSummaryResponse> Items,
    string? NextCursor);
