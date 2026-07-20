using System.Text.Json;
using Application.Common.Interfaces;
using Application.Features.Chat.Common.Messages.GetConversationMessages.DTOs;
using Application.Features.Chat.Common.Messages.Send.DTOs;
using Application.Features.Contracts.Common.Internal;
using Domain.Entities;

namespace Application.Features.ReportContracts.Common.Internal;

internal static class ReportContractSystemMessages
{
    public const string CreatedEvent = "created";
    public const string UpdatedEvent = "updated";
    public const string ResolvedEvent = "resolved";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Task<Message?> AddAsync(
        IApplicationDbContext context,
        ReportContract report,
        string eventType,
        string? actorName,
        string? actorRole,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var metadata = JsonSerializer.Serialize(new ReportContractMessageMetadata(
            "reportContract",
            report.ReportContractId,
            report.ContractId,
            eventType,
            actorName,
            actorRole,
            report.IssueType,
            report.DesiredResolution,
            report.Description,
            report.Status,
            report.ResolutionAction,
            report.Explanation,
            report.ProposedResolution,
            report.RejectReason), JsonOptions);

        return ContractConversationEvents.AddSystemMessageAsync(
            context,
            report.ContractId,
            BuildPreviewContent(eventType, actorName, actorRole),
            now,
            cancellationToken,
            metadata);
    }

    public static MessageResponse ToRealtimePayload(Message message) =>
        new(
            message.MessagesId,
            message.ConversationsId,
            message.SenderUserId,
            message.MessageType,
            message.Content,
            message.ReplyToMessageId,
            message.Metadata,
            message.ClientMessageId,
            message.SentAt,
            message.EditedAt,
            false,
            Array.Empty<MessageAttachmentResponse>());

    private static string BuildPreviewContent(string eventType, string? actorName, string? actorRole)
    {
        var actor = !string.IsNullOrWhiteSpace(actorName) ? actorName : actorRole ?? "A contract participant";
        return eventType switch
        {
            CreatedEvent => $"{actor} raised an issue report.",
            UpdatedEvent => $"{actor} updated an issue report.",
            ResolvedEvent => "An issue report has been resolved.",
            _ => "An issue report was updated."
        };
    }

    private sealed record ReportContractMessageMetadata(
        string Kind,
        Guid ReportId,
        Guid ContractId,
        string EventType,
        string? ActorName,
        string? ActorRole,
        int IssueType,
        string DesiredResolution,
        string Description,
        int Status,
        int? ResolutionAction,
        string? Explanation,
        string? ProposedResolution,
        string? RejectReason);
}
