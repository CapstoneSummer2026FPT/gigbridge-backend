using System.Text;
using System.Text.Json;
using Application.Common.Exceptions;
using Application.Features.Chat.Common.Conversations.GetMine.DTOs;
using MediatR;

namespace Application.Features.Chat.Common.Conversations.GetMine.Queries;

public sealed class GetConversationSummaryPageQueryHandler(IMediator mediator)
    : IRequestHandler<GetConversationSummaryPageQuery, ConversationSummaryPageResponse>
{
    public async Task<ConversationSummaryPageResponse> Handle(
        GetConversationSummaryPageQuery request,
        CancellationToken cancellationToken)
    {
        var filterCount = new[] { request.ContractId, request.DisputeId, request.ProposalId, request.JobPostId }
            .Count(value => value.HasValue);
        if (filterCount > 1)
        {
            throw new BadRequestException("Only one conversation summary filter may be supplied.");
        }

        var pageSize = Math.Clamp(request.PageSize, 1, 50);
        var cursor = DecodeCursor(request.Cursor);
        var rows = await mediator.Send(new GetMyConversationsQuery(
            request.UserId,
            cursor,
            pageSize + 1,
            request.ContractId,
            request.DisputeId,
            request.ProposalId,
            request.JobPostId), cancellationToken);
        var items = rows.Take(pageSize).ToList();
        var nextCursor = rows.Count > pageSize && items.Count > 0
            ? EncodeCursor(items[^1])
            : null;
        return new ConversationSummaryPageResponse(items, nextCursor);
    }

    private static ConversationPageCursor? DecodeCursor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(value));
            return JsonSerializer.Deserialize<ConversationPageCursor>(json)
                ?? throw new BadRequestException("Invalid conversation cursor.");
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            throw new BadRequestException("Invalid conversation cursor.");
        }
    }

    private static string EncodeCursor(ConversationSummaryResponse item)
    {
        var cursor = new ConversationPageCursor(item.LastMessageAt ?? item.CreatedAt, item.ConversationId);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(cursor)));
    }
}
