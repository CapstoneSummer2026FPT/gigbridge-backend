using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Files;
using Application.Common.Interfaces.Media;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Chat.Interfaces;
using Application.Common.InternalServices.Notifications.Interfaces;
using Application.Common.Models.Files;
using Application.Features.Contracts.Common.Internal;
using Application.Features.Contracts.ProductHandoffs.Common;
using Application.Features.Contracts.ProductHandoffs.Common.DTOs;
using Domain.Entities;
using Domain.Enums.Contracts;
using Domain.Enums.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Contracts.ProductHandoffs.Submit.Commands;

public sealed class SubmitContractProductHandoffCommandHandler :
    IRequestHandler<SubmitContractProductHandoffCommand, ContractProductHandoffResponse>
{
    private const string UploadFolder = "contract-products";

    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IMediaService _mediaService;
    private readonly INotificationService _notificationService;
    private readonly IChatRealtimeNotifier _chatRealtimeNotifier;
    private readonly IWorkspaceUploadFilePolicy _uploadFilePolicy;
    private readonly ILogger<SubmitContractProductHandoffCommandHandler>? _logger;

    public SubmitContractProductHandoffCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IMediaService mediaService,
        INotificationService notificationService,
        IChatRealtimeNotifier chatRealtimeNotifier,
        IWorkspaceUploadFilePolicy uploadFilePolicy,
        ILogger<SubmitContractProductHandoffCommandHandler>? logger = null)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _mediaService = mediaService;
        _notificationService = notificationService;
        _chatRealtimeNotifier = chatRealtimeNotifier;
        _uploadFilePolicy = uploadFilePolicy;
        _logger = logger;
    }

    public async Task<ContractProductHandoffResponse> Handle(
        SubmitContractProductHandoffCommand command,
        CancellationToken cancellationToken)
    {
        ValidateRequest(command);

        var validatedFiles = command.File is null
            ? new ValidatedWorkspaceUploadBatch(Array.Empty<ValidatedWorkspaceUploadFile>())
            : await _uploadFilePolicy.ValidateBatchAsync(
                [new WorkspaceUploadFile(
                    command.File.Content,
                    command.File.FileName,
                    command.File.ContentType,
                    command.File.Length)],
                1,
                cancellationToken);

        try
        {
            var contract = await ContractProductHandoffAccess.GetActiveContractAsync(
                _context,
                command.ContractId,
                cancellationToken);

            await ContractProductHandoffAccess.EnsureClientAsync(
                _context,
                contract,
                command.UserId,
                cancellationToken);

            var now = _dateTimeService.UtcNow;

            // Combined into a single round trip: one tracked fetch of every handoff for this
            // contract, then compute both "which are current" and "next version" in memory
            // instead of two separate DB queries.
            var allHandoffs = await _context.Set<ContractProductHandoff>()
                .Where(handoff => handoff.ContractsId == contract.ContractsId)
                .ToListAsync(cancellationToken);

            foreach (var currentHandoff in allHandoffs.Where(h => h.IsCurrent))
            {
                currentHandoff.IsCurrent = false;
            }

            var nextVersion = allHandoffs.Count > 0 ? allHandoffs.Max(h => h.Version) : 0;

            var handoff = new ContractProductHandoff
            {
                ContractProductHandoffId = Guid.NewGuid(),
                ContractsId = contract.ContractsId,
                SubmittedByUserId = command.UserId,
                Note = NormalizeNote(command.Note),
                Version = nextVersion + 1,
                IsCurrent = true,
                CreatedAt = now
            };

            string? uploadedFileUrl = null;
            try
            {
                if (validatedFiles.Count == 1)
                {
                    var file = validatedFiles[0];
                    uploadedFileUrl = await _mediaService.UploadFileAsync(
                        file.Content,
                        file.FileName,
                        file.ContentType,
                        UploadFolder,
                        cancellationToken);

                    handoff.SourceType = (int)ContractProductHandoffSourceType.File;
                    handoff.FileName = file.FileName;
                    handoff.FileUrl = uploadedFileUrl;
                    handoff.MimeType = file.ContentType;
                    handoff.FileSizeBytes = file.Length;
                }
                else
                {
                    handoff.SourceType = (int)ContractProductHandoffSourceType.Link;
                    handoff.ExternalUrl = command.ExternalUrl!.Trim();
                }

                _context.Set<ContractProductHandoff>().Add(handoff);
                contract.UpdatedAt = now;

                var materialLabel =
                    handoff.SourceType == (int)ContractProductHandoffSourceType.File
                        ? handoff.FileName
                        : handoff.ExternalUrl;
                var systemMessage = await ContractConversationEvents.AddSystemMessageAsync(
                    _context,
                    contract.ContractsId,
                    $"Client sent product materials: {materialLabel}.",
                    now,
                    cancellationToken);

                await NotifyFreelancerAsync(contract, handoff, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);

                var participantUserIds =
                    await ContractProductHandoffAccess.GetParticipantUserIdsAsync(
                        _context,
                        contract,
                        cancellationToken);

                if (participantUserIds.Count > 0)
                {
                    await _chatRealtimeNotifier.SendUsersEventAsync(
                        participantUserIds,
                        "ProductHandoffUpdated",
                        ContractProductHandoffMapper.ToResponse(handoff),
                        cancellationToken);
                }

                if (systemMessage is not null)
                {
                    var messagePayload = ContractConversationEvents.ToRealtimePayload(systemMessage);

                    if (participantUserIds.Count > 0)
                    {
                        await _chatRealtimeNotifier.SendUsersEventAsync(
                            participantUserIds,
                            "ReceiveMessage",
                            messagePayload,
                            cancellationToken);
                    }

                    await _chatRealtimeNotifier.SendConversationEventAsync(
                        systemMessage.ConversationsId,
                        "ReceiveMessage",
                        messagePayload,
                        cancellationToken);
                }

                return ContractProductHandoffMapper.ToResponse(handoff);
            }
            catch
            {
                if (!string.IsNullOrWhiteSpace(uploadedFileUrl))
                {
                    try
                    {
                        await _mediaService.DeleteFileAsync(
                            uploadedFileUrl,
                            UploadFolder,
                            CancellationToken.None);
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        _logger?.LogWarning(
                            exception,
                            "Failed to roll back product handoff file at {FileUrl}.",
                            uploadedFileUrl);
                    }
                }

                throw;
            }
        }
        finally
        {
            await validatedFiles.DisposeAsync();
        }
    }

    private static void ValidateRequest(SubmitContractProductHandoffCommand command)
    {
        var hasFile = command.File is not null;
        var hasExternalUrl = !string.IsNullOrWhiteSpace(command.ExternalUrl);

        if (hasFile == hasExternalUrl)
        {
            throw new BadRequestException("Provide exactly one product file or external URL.");
        }

        if (command.Note is not null && command.Note.Length > 2000)
        {
            throw new BadRequestException("Product handoff note exceeds 2000 characters.");
        }

        if (command.File is null)
        {
            if (!Uri.TryCreate(command.ExternalUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new BadRequestException("External URL must be a valid HTTP or HTTPS URL.");
            }

            return;
        }

        // File content, type, archive entries, and size are validated by the shared policy.
    }

    private static string? NormalizeNote(string? note)
    {
        return string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    private async Task NotifyFreelancerAsync(
        Contract contract,
        ContractProductHandoff handoff,
        CancellationToken cancellationToken)
    {
        if (!contract.FreelancerProfilesId.HasValue)
        {
            return;
        }

        var freelancerUserId = await _context.Set<FreelancerProfile>()
            .AsNoTracking()
            .Where(profile => profile.FreelancerProfilesId == contract.FreelancerProfilesId.Value)
            .Select(profile => (Guid?)profile.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!freelancerUserId.HasValue)
        {
            return;
        }

        await _notificationService.CreateNotificationAsync(
            freelancerUserId.Value,
            NotificationType.SystemAlert,
            "Product materials received",
            $"Client sent product materials for contract '{contract.Title}'.",
            handoff.ContractProductHandoffId,
            "ContractProductHandoff",
            cancellationToken);
    }
}
