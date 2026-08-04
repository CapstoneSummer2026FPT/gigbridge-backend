using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Disputes.Common.DTOs;
using Application.Features.Disputes.Common.Internal;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Disputes.Evidence.Add.Commands;

public sealed class AddDisputeEvidenceCommandHandler :
    IRequestHandler<AddDisputeEvidenceCommand, IReadOnlyList<DisputeEvidenceResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IMediaService _mediaService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<AddDisputeEvidenceCommandHandler> _logger;

    public AddDisputeEvidenceCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IMediaService mediaService,
        INotificationService notificationService,
        ILogger<AddDisputeEvidenceCommandHandler> logger)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _mediaService = mediaService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DisputeEvidenceResponse>> Handle(
        AddDisputeEvidenceCommand command,
        CancellationToken cancellationToken)
    {
        var contract = await DisputeAccess.GetContractAsync(_context, command.ContractId, cancellationToken);
        var participants = await DisputeAccess.EnsureParticipantAsync(_context, contract, command.UserId, cancellationToken);
        var dispute = await _context.Set<Dispute>()
            .FirstOrDefaultAsync(item =>
                    item.DisputesId == command.DisputeId && item.ContractsId == command.ContractId,
                cancellationToken)
            ?? throw new NotFoundException("Dispute does not exist.");

        if (!DisputeAccess.ActiveStatuses.Contains(dispute.Status))
            throw new BadRequestException("Evidence can only be added while the dispute is active.");

        DisputeEvidenceSupport.ValidateBatch(command.Files);
        var now = _dateTimeService.UtcNow;
        var changed = command.RequestEvidenceId.HasValue
            ? await FulfillRequestAsync(command, dispute, now, cancellationToken)
            : await CreateVoluntaryEvidenceAsync(command, dispute, now, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        var otherPartyId = participants.GetOtherParty(command.UserId);
        if (otherPartyId.HasValue)
        {
            try
            {
                await _notificationService.CreateNotificationAsync(
                    otherPartyId.Value,
                    NotificationType.DisputeUpdate,
                    command.RequestEvidenceId.HasValue ? "Requested evidence submitted" : "New dispute evidence",
                    $"New evidence was added to the dispute for contract '{contract.Title}'.",
                    contract.ContractsId,
                    nameof(Contract),
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "Dispute evidence was saved, but notification delivery failed.");
            }
        }

        return changed.Select(ToResponse).ToList();
    }

    private async Task<List<DisputeEvidence>> FulfillRequestAsync(
        AddDisputeEvidenceCommand command,
        Dispute dispute,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var placeholder = await _context.Set<DisputeEvidence>()
            .FirstOrDefaultAsync(item =>
                    item.DisputeEvidenceId == command.RequestEvidenceId &&
                    item.DisputesId == dispute.DisputesId,
                cancellationToken)
            ?? throw new NotFoundException("Evidence request does not exist.");

        if (!placeholder.IsRequestedByAdmin ||
            !placeholder.RequestedByAdminId.HasValue ||
            placeholder.UploadedById.HasValue ||
            placeholder.IsRequestFulfilled)
        {
            throw new BadRequestException("This evidence request has already been fulfilled or is not a request placeholder.");
        }
        if (placeholder.Deadline.HasValue && placeholder.Deadline.Value < now)
            throw new BadRequestException("The evidence request deadline has passed.");

        var expectedUserId = placeholder.RequestTarget switch
        {
            (int)EvidenceRequestTarget.Reporter => dispute.InitiatorId,
            (int)EvidenceRequestTarget.Respondent when dispute.RespondentId.HasValue => dispute.RespondentId.Value,
            _ => Guid.Empty
        };
        if (expectedUserId == Guid.Empty || expectedUserId != command.UserId)
            throw new ForbiddenAccessException("You are not the target of this evidence request.");

        var uploaded = new List<DisputeEvidence>(command.Files.Count);
        foreach (var file in command.Files)
        {
            uploaded.Add(await DisputeEvidenceSupport.UploadAsync(
                _mediaService,
                file,
                dispute.DisputesId,
                command.UserId,
                now,
                cancellationToken));
        }

        var first = uploaded[0];
        placeholder.UploadedById = command.UserId;
        placeholder.FileName = first.FileName;
        placeholder.FileUrl = first.FileUrl;
        placeholder.FileSize = first.FileSize;
        placeholder.CreatedAt = now;
        placeholder.IsRequestFulfilled = true;

        var changed = new List<DisputeEvidence> { placeholder };
        foreach (var child in uploaded.Skip(1))
        {
            child.IsRequestedByAdmin = true;
            child.RequestGroupId = placeholder.RequestGroupId;
            child.RequestTarget = placeholder.RequestTarget;
            child.IsRequestFulfilled = true;
            _context.Set<DisputeEvidence>().Add(child);
            changed.Add(child);
        }

        return changed;
    }

    private async Task<List<DisputeEvidence>> CreateVoluntaryEvidenceAsync(
        AddDisputeEvidenceCommand command,
        Dispute dispute,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var evidence = new List<DisputeEvidence>(command.Files.Count);
        foreach (var file in command.Files)
        {
            var uploaded = await DisputeEvidenceSupport.UploadAsync(
                _mediaService,
                file,
                dispute.DisputesId,
                command.UserId,
                now,
                cancellationToken);
            uploaded.IsRequestedByAdmin = false;
            evidence.Add(uploaded);
        }
        _context.Set<DisputeEvidence>().AddRange(evidence);
        return evidence;
    }

    private static DisputeEvidenceResponse ToResponse(DisputeEvidence evidence) => new(
        evidence.DisputeEvidenceId,
        evidence.UploadedById,
        evidence.FileName,
        evidence.FileSize,
        evidence.Description,
        evidence.CreatedAt,
        evidence.IsRequestedByAdmin,
        evidence.RequestGroupId,
        evidence.RequestedByAdminId,
        evidence.RequestedAt,
        evidence.Deadline,
        evidence.RequestTarget,
        evidence.IsRequestFulfilled,
        evidence.ReviewedByAdminId,
        evidence.ReviewedAt,
        evidence.ReviewNote);
}
