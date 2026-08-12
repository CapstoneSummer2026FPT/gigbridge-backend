using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Chat.Common.Interfaces;
using Application.Features.Chat.Common.Messages.Send.Commands;
using Application.Features.Chat.Common.Messages.Send.DTOs;
using Domain.Entities;
using Domain.Enums.Chat;
using Domain.Enums.Contracts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Chat.Common.Messages.CreateGoogleMeet;

public sealed class CreateGoogleMeetMessageCommandHandler(
    IApplicationDbContext context,
    IGoogleMeetOAuthService meetOAuth,
    IGoogleMeetApiClient meetApi,
    ISender sender) : IRequestHandler<CreateGoogleMeetMessageCommand, MessageResponse>
{
    public async Task<MessageResponse> Handle(
        CreateGoogleMeetMessageCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        if (request.ConversationId == Guid.Empty)
            throw new BadRequestException("ConversationId is required.");
        if (string.IsNullOrWhiteSpace(request.ClientMessageId))
            throw new BadRequestException("clientMessageId is required.");

        await EnsureConversationIsWritableAsync(
            request.ConversationId,
            command.UserId,
            cancellationToken);

        var accessToken = await meetOAuth.GetAccessTokenAsync(command.UserId, cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new BadRequestException("Connect your Google account before creating a Google Meet room.");

        var meet = await meetApi.CreateSpaceAsync(accessToken, cancellationToken);
        if (!meet.IsSuccess || string.IsNullOrWhiteSpace(meet.MeetingUri))
            throw new BadRequestException(GetCreationFailureMessage(meet.FailureCode));

        return await sender.Send(
            new SendMessageCommand(
                command.UserId,
                new SendMessageRequest(
                    request.ConversationId,
                    request.ClientMessageId.Trim(),
                    meet.MeetingUri,
                    null)),
            cancellationToken);
    }

    private async Task EnsureConversationIsWritableAsync(
        Guid conversationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var conversation = await context.Set<Conversation>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.ConversationsId == conversationId, cancellationToken);
        if (conversation is null)
            throw new NotFoundException("Conversation does not exist.");
        if (conversation.Status != (int)ConversationStatus.Active)
            throw new BadRequestException("Conversation is not active.");

        var participant = await context.Set<ConversationParticipant>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item =>
                item.ConversationsId == conversationId &&
                item.UserId == userId &&
                item.LeftAt == null,
                cancellationToken);
        if (participant is null)
            throw new ForbiddenAccessException("You are not a participant in this conversation.");

        if (conversation.ConversationType == (int)ConversationType.ContractWorkroom &&
            participant.ParticipantRole == (int)ParticipantRole.Admin)
            throw new ForbiddenAccessException("Administrators may only read Workspace conversations.");

        if (conversation.ConversationType != (int)ConversationType.ContractWorkroom ||
            !conversation.ContractsId.HasValue)
            return;

        var contractStatus = await context.Set<Contract>()
            .AsNoTracking()
            .Where(contract => contract.ContractsId == conversation.ContractsId.Value)
            .Select(contract => (int?)contract.Status)
            .FirstOrDefaultAsync(cancellationToken);
        if (contractStatus == (int)ContractStatus.Disputed)
            throw new BadRequestException(
                "This contract is currently under dispute. Please continue communication in the dispute conversation.");
    }

    private static string GetCreationFailureMessage(string? failureCode) => failureCode switch
    {
        "timeout" => "Google Meet room creation timed out. Please try again.",
        "network_error" => "Google Meet could not be reached. Please try again.",
        "meet_server_error" => "Google Meet is temporarily unavailable. Please try again.",
        _ => "Unable to create a Google Meet room. Reconnect your Google account and try again."
    };
}
