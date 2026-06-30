using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Contracts.Common.Internal;
using Application.Features.Contracts.ProductHandoffs.Common;
using Application.Features.Contracts.ProductHandoffs.Common.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.ProductHandoffs.Submit.Commands;

public sealed class SubmitContractProductHandoffCommandHandler :
    IRequestHandler<SubmitContractProductHandoffCommand, ContractProductHandoffResponse>
{
    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe",
        ".bat",
        ".cmd",
        ".sh"
    };

    private const long MaxFileSizeBytes = 100 * 1024 * 1024;

    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IMediaService _mediaService;
    private readonly INotificationService _notificationService;
    private readonly IChatRealtimeNotifier _chatRealtimeNotifier;

    public SubmitContractProductHandoffCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IMediaService mediaService,
        INotificationService notificationService,
        IChatRealtimeNotifier chatRealtimeNotifier)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _mediaService = mediaService;
        _notificationService = notificationService;
        _chatRealtimeNotifier = chatRealtimeNotifier;
    }

    public async Task<ContractProductHandoffResponse> Handle(
        SubmitContractProductHandoffCommand command,
        CancellationToken cancellationToken)
    {
        ValidateRequest(command);

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
        var currentHandoffs = await _context.Set<ContractProductHandoff>()
            .Where(handoff => handoff.ContractsId == contract.ContractsId && handoff.IsCurrent)
            .ToListAsync(cancellationToken);

        foreach (var currentHandoff in currentHandoffs)
        {
            currentHandoff.IsCurrent = false;
        }

        var nextVersion = await _context.Set<ContractProductHandoff>()
            .AsNoTracking()
            .Where(handoff => handoff.ContractsId == contract.ContractsId)
            .Select(handoff => (int?)handoff.Version)
            .MaxAsync(cancellationToken) ?? 0;

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

        if (command.File is not null)
        {
            var fileUrl = await _mediaService.UploadFileAsync(
                command.File.Content,
                command.File.FileName,
                command.File.ContentType,
                "contract-products",
                cancellationToken);

            handoff.SourceType = (int)ContractProductHandoffSourceType.File;
            handoff.FileName = command.File.FileName.Trim();
            handoff.FileUrl = fileUrl;
            handoff.MimeType = command.File.ContentType.Trim();
            handoff.FileSizeBytes = command.File.Length;
        }
        else
        {
            handoff.SourceType = (int)ContractProductHandoffSourceType.Link;
            handoff.ExternalUrl = command.ExternalUrl!.Trim();
        }

        _context.Set<ContractProductHandoff>().Add(handoff);
        contract.UpdatedAt = now;

        await ContractConversationEvents.AddSystemMessageAsync(
            _context,
            contract.ContractsId,
            "Client sent product materials.",
            now,
            cancellationToken);

        await NotifyFreelancerAsync(contract, handoff, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        var participantUserIds = await ContractProductHandoffAccess.GetParticipantUserIdsAsync(
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

        return ContractProductHandoffMapper.ToResponse(handoff);
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

        if (command.File.Length <= 0 || command.File.Length > MaxFileSizeBytes)
        {
            throw new BadRequestException("Product file size is invalid.");
        }

        if (string.IsNullOrWhiteSpace(command.File.FileName))
        {
            throw new BadRequestException("Product file name is required.");
        }

        var extension = Path.GetExtension(command.File.FileName);
        if (!string.IsNullOrWhiteSpace(extension) && BlockedExtensions.Contains(extension))
        {
            throw new BadRequestException("Product file extension is not allowed.");
        }
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
