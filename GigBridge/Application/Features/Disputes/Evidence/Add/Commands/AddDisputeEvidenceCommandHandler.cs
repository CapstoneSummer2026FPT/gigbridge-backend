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
        var contract = await DisputeAccess.GetContractAsync(
            _context,
            command.ContractId,
            cancellationToken);
        var participants = await DisputeAccess.EnsureParticipantAsync(
            _context,
            contract,
            command.UserId,
            cancellationToken);

        var dispute = await _context.Set<Dispute>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item =>
                    item.DisputesId == command.DisputeId &&
                    item.ContractsId == command.ContractId,
                cancellationToken)
            ?? throw new NotFoundException("Dispute does not exist.");

        if (!DisputeAccess.ActiveStatuses.Contains(dispute.Status))
        {
            throw new BadRequestException(
                "Evidence can only be added while the dispute is open or under review.");
        }

        DisputeEvidenceSupport.ValidateBatch(command.Files);
        var now = _dateTimeService.UtcNow;
        var evidences = new List<DisputeEvidence>(command.Files.Count);
        foreach (var file in command.Files)
        {
            evidences.Add(await DisputeEvidenceSupport.UploadAsync(
                _mediaService,
                file,
                dispute.DisputesId,
                command.UserId,
                now,
                cancellationToken));
        }

        _context.Set<DisputeEvidence>().AddRange(evidences);
        await _context.SaveChangesAsync(cancellationToken);

        var otherPartyId = participants.GetOtherParty(command.UserId);
        if (otherPartyId.HasValue)
        {
            try
            {
                await _notificationService.CreateNotificationAsync(
                    otherPartyId.Value,
                    NotificationType.DisputeUpdate,
                    "New dispute evidence",
                    $"New evidence was added to the dispute for contract '{contract.Title}'.",
                    contract.ContractsId,
                    nameof(Contract),
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(
                    exception,
                    "Dispute evidence was saved, but notification delivery to user {UserId} failed.",
                    otherPartyId.Value);
            }
        }

        return evidences
            .Select(evidence => new DisputeEvidenceResponse(
                evidence.DisputeEvidenceId,
                evidence.UploadedById,
                evidence.FileName,
                evidence.FileSize,
                evidence.Description,
                evidence.CreatedAt))
            .ToList();
    }
}
