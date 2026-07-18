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

namespace Application.Features.Disputes.Create.Commands;

public sealed class CreateDisputeCommandHandler :
    IRequestHandler<CreateDisputeCommand, DisputeResponse>
{
    private const long MaxEvidenceFileSizeBytes = 100 * 1024 * 1024;

    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly INotificationService _notificationService;
    private readonly IMediaService _mediaService;
    private readonly ILogger<CreateDisputeCommandHandler> _logger;

    public CreateDisputeCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        INotificationService notificationService,
        IMediaService mediaService,
        ILogger<CreateDisputeCommandHandler> logger)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _notificationService = notificationService;
        _mediaService = mediaService;
        _logger = logger;
    }

    public async Task<DisputeResponse> Handle(
        CreateDisputeCommand command,
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
        DisputeAccess.EnsureCreationAllowed(contract);

        var hasActiveDispute = await _context.Set<Dispute>()
            .AsNoTracking()
            .AnyAsync(dispute =>
                    dispute.ContractsId == command.ContractId &&
                    DisputeAccess.ActiveStatuses.Contains(dispute.Status),
                cancellationToken);

        if (hasActiveDispute)
        {
            throw new ConflictException("An active dispute already exists for this contract.");
        }

        string? milestoneTitle = null;
        if (command.MilestoneId.HasValue)
        {
            var milestone = await _context.Set<Milestone>()
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item => item.MilestonesId == command.MilestoneId.Value,
                    cancellationToken)
                ?? throw new NotFoundException("Milestone does not exist.");

            if (milestone.ContractsId != command.ContractId)
            {
                throw new BadRequestException("The specified milestone does not belong to this contract.");
            }

            milestoneTitle = milestone.Title;
        }

        var initiatorName = await _context.Set<User>()
            .AsNoTracking()
            .Where(user => user.UserId == command.UserId)
            .Select(user => user.FullName)
            .FirstOrDefaultAsync(cancellationToken);

        var now = _dateTimeService.UtcNow;
        DisputeEvidence? evidence = null;
        if (command.Evidence is not null)
        {
            var safeFileName = ValidateEvidenceFile(command.Evidence);
            var fileUrl = await _mediaService.UploadFileAsync(
                command.Evidence.Content,
                safeFileName,
                command.Evidence.ContentType,
                "disputes",
                cancellationToken);

            evidence = new DisputeEvidence
            {
                DisputeEvidenceId = Guid.NewGuid(),
                DisputesId = Guid.Empty,
                UploadedById = command.UserId,
                FileName = safeFileName,
                FileUrl = fileUrl,
                FileSize = command.Evidence.Length,
                Description = command.EvidenceDescription?.Trim(),
                CreatedAt = now
            };
        }

        var dispute = new Dispute
        {
            DisputesId = Guid.NewGuid(),
            ContractsId = command.ContractId,
            InitiatorId = command.UserId,
            RespondentId = participants.GetOtherParty(command.UserId),
            MilestonesId = command.MilestoneId,
            Reason = command.Reason.Trim(),
            Status = (int)DisputeStatus.Open,
            Resolution = null,
            ResolutionNote = null,
            ResolvedByAdminId = null,
            ResolvedAt = null,
            CreatedAt = now,
            UpdatedAt = null,
            OpenedAt = now
        };

        _context.Set<Dispute>().Add(dispute);
        if (evidence is not null)
        {
            evidence.DisputesId = dispute.DisputesId;
            _context.Set<DisputeEvidence>().Add(evidence);
        }

        await _context.SaveChangesAsync(cancellationToken);

        var initiatorRole = participants.GetRole(command.UserId)!;
        var otherPartyId = participants.GetOtherParty(command.UserId);
        if (otherPartyId.HasValue)
        {
            try
            {
                await _notificationService.CreateNotificationAsync(
                    otherPartyId.Value,
                    NotificationType.DisputeUpdate,
                    "A dispute has been opened",
                    $"A dispute has been opened on contract '{contract.Title}' by the {initiatorRole}.",
                    contract.ContractsId,
                    nameof(Contract),
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(
                    exception,
                    "Dispute {DisputeId} was created, but notification delivery to user {UserId} failed.",
                    dispute.DisputesId,
                    otherPartyId.Value);
            }
        }

        return BuildDisputeResponse(
            dispute,
            evidence,
            initiatorName,
            initiatorRole,
            otherPartyId.HasValue ? participants.GetRole(otherPartyId.Value) : null,
            milestoneTitle);
    }

    private static string ValidateEvidenceFile(CreateDisputeFile file)
    {
        if (file.Length <= 0)
        {
            throw new BadRequestException("Evidence file is empty.");
        }

        if (file.Length > MaxEvidenceFileSizeBytes)
        {
            throw new BadRequestException("Evidence file size exceeds the maximum allowed size of 100 MB.");
        }

        if (string.IsNullOrWhiteSpace(file.FileName))
        {
            throw new BadRequestException("Evidence file name is required.");
        }

        var safeFileName = Path.GetFileName(file.FileName.Trim());
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            throw new BadRequestException("Evidence file name is invalid.");
        }

        if (safeFileName.Length > 500)
        {
            throw new BadRequestException("Evidence file name must not exceed 500 characters.");
        }

        return safeFileName;
    }

    private static DisputeResponse BuildDisputeResponse(
        Dispute dispute,
        DisputeEvidence? evidence,
        string? initiatorName,
        string initiatorRole,
        string? respondentRole,
        string? milestoneTitle)
    {
        var evidences = evidence is null
            ? Array.Empty<DisputeEvidenceResponse>()
            :
            [
                new DisputeEvidenceResponse(
                    evidence.DisputeEvidenceId,
                    evidence.UploadedById,
                    evidence.FileName,
                    evidence.FileSize,
                    evidence.Description,
                    evidence.CreatedAt)
            ];

        return new DisputeResponse(
            dispute.DisputesId,
            dispute.ContractsId,
            dispute.InitiatorId,
            initiatorName,
            initiatorRole,
            dispute.RespondentId,
            null,
            respondentRole,
            dispute.MilestonesId,
            milestoneTitle,
            dispute.RelatedReportId,
            dispute.Title,
            dispute.Description,
            dispute.Reason,
            dispute.ClaimedAmount,
            dispute.RequestedResolution,
            dispute.Status,
            dispute.Resolution,
            null,
            dispute.ResolutionNote,
            dispute.ResolvedAt,
            dispute.CreatedAt,
            dispute.UpdatedAt,
            dispute.OpenedAt,
            evidences);
    }
}
