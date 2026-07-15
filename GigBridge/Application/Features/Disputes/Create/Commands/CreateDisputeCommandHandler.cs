using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Disputes.Common.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Disputes.Create.Commands;

public sealed class CreateDisputeCommandHandler :
    IRequestHandler<CreateDisputeCommand, DisputeResponse>
{
    private const long MaxEvidenceFileSizeBytes = 100 * 1024 * 1024; // 100 MB

    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly INotificationService _notificationService;
    private readonly IMediaService? _mediaService;

    public CreateDisputeCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        INotificationService notificationService,
        IMediaService? mediaService = null)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _notificationService = notificationService;
        _mediaService = mediaService;
    }

    public async Task<DisputeResponse> Handle(
        CreateDisputeCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Load contract
        var contract = await _context.Set<Contract>()
            .FirstOrDefaultAsync(c => c.ContractsId == command.ContractId, cancellationToken)
            ?? throw new NotFoundException("Contract does not exist.");

        // 2. Validate current user is a participant of this contract
        var initiatorRole = await ResolveParticipantRoleAsync(contract, command.UserId, cancellationToken);

        // 3. Check no active dispute exists
        var activeDispute = await _context.Set<Dispute>()
            .AnyAsync(d =>
                d.ContractsId == command.ContractId &&
                (d.Status == (int)DisputeStatus.Open ||
                 d.Status == (int)DisputeStatus.UnderReview),
                cancellationToken);

        if (activeDispute)
        {
            throw new ConflictException("An active dispute already exists for this contract.");
        }

        // 4. Validate milestone if provided
        if (command.MilestoneId.HasValue)
        {
            var milestone = await _context.Set<Milestone>()
                .FirstOrDefaultAsync(m => m.MilestonesId == command.MilestoneId.Value, cancellationToken);

            if (milestone is null)
            {
                throw new NotFoundException("Milestone does not exist.");
            }

            if (milestone.ContractsId != command.ContractId)
            {
                throw new BadRequestException("The specified milestone does not belong to this contract.");
            }
        }

        var now = _dateTimeService.UtcNow;

        // 5. Upload evidence if provided
        DisputeEvidence? evidence = null;
        if (command.Evidence is not null)
        {
            ValidateEvidenceFile(command.Evidence);

            if (_mediaService is null)
            {
                throw new InvalidOperationException("MediaService is not configured for file uploads.");
            }

            var fileUrl = await _mediaService.UploadFileAsync(
                command.Evidence.Content,
                command.Evidence.FileName,
                command.Evidence.ContentType,
                "disputes",
                cancellationToken);

            evidence = new DisputeEvidence
            {
                DisputeEvidenceId = Guid.NewGuid(),
                DisputesId = Guid.Empty, // Will be set after dispute is created
                UploadedById = command.UserId,
                FileName = command.Evidence.FileName.Trim(),
                FileUrl = fileUrl,
                FileSize = command.Evidence.Length,
                Description = command.EvidenceDescription?.Trim(),
                CreatedAt = now
            };
        }

        // 6. Create dispute
        var dispute = new Dispute
        {
            DisputesId = Guid.NewGuid(),
            ContractsId = command.ContractId,
            InitiatorId = command.UserId,
            MilestonesId = command.MilestoneId,
            Reason = command.Reason.Trim(),
            Status = (int)DisputeStatus.Open,
            Resolution = null,
            ResolutionNote = null,
            ResolvedByAdminId = null,
            ResolvedAt = null,
            CreatedAt = now,
            UpdatedAt = null
        };

        _context.Set<Dispute>().Add(dispute);

        // 7. Create evidence record if uploaded
        if (evidence is not null)
        {
            evidence.DisputesId = dispute.DisputesId;
            _context.Set<DisputeEvidence>().Add(evidence);
        }

        await _context.SaveChangesAsync(cancellationToken);

        // 8. Send notification to the other party
        var otherPartyId = await ResolveOtherPartyUserIdAsync(contract, command.UserId, cancellationToken);

        if (otherPartyId.HasValue)
        {
            var roleLabel = initiatorRole == "Client" ? "Client" : "Freelancer";
            await _notificationService.CreateNotificationAsync(
                otherPartyId.Value,
                NotificationType.DisputeUpdate,
                "A dispute has been opened",
                $"A dispute has been opened on contract '{contract.Title}' by the {roleLabel}.",
                contract.ContractsId,
                "Contract",
                cancellationToken);
        }

        // 9. Build response
        return BuildDisputeResponse(dispute, evidence, command.UserId, initiatorRole, contract);
    }

    private static void ValidateEvidenceFile(CreateDisputeFile file)
    {
        if (file.Length <= 0)
        {
            throw new BadRequestException("Evidence file is empty.");
        }

        if (file.Length > MaxEvidenceFileSizeBytes)
        {
            throw new BadRequestException($"Evidence file size exceeds the maximum allowed size of {MaxEvidenceFileSizeBytes / (1024 * 1024)} MB.");
        }

        if (string.IsNullOrWhiteSpace(file.FileName))
        {
            throw new BadRequestException("Evidence file name is required.");
        }
    }

    private async Task<string> ResolveParticipantRoleAsync(
        Contract contract,
        Guid userId,
        CancellationToken cancellationToken)
    {
        // Check if user is the client
        var clientProfile = await _context.Set<ClientProfile>()
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (clientProfile is not null && clientProfile.ClientProfilesId == contract.ClientProfilesId)
        {
            return "Client";
        }

        // Check if user is the freelancer
        if (contract.FreelancerProfilesId.HasValue)
        {
            var freelancerProfile = await _context.Set<FreelancerProfile>()
                .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

            if (freelancerProfile is not null &&
                freelancerProfile.FreelancerProfilesId == contract.FreelancerProfilesId.Value)
            {
                return "Freelancer";
            }
        }

        throw new ForbiddenAccessException("Only the contract client or freelancer can open a dispute.");
    }

    private async Task<Guid?> ResolveOtherPartyUserIdAsync(
        Contract contract,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        // If current user is the client, notify freelancer
        var clientProfile = await _context.Set<ClientProfile>()
            .FirstOrDefaultAsync(p => p.UserId == currentUserId, cancellationToken);

        if (clientProfile is not null && clientProfile.ClientProfilesId == contract.ClientProfilesId)
        {
            if (!contract.FreelancerProfilesId.HasValue)
                return null;

            var freelancerUser = await _context.Set<FreelancerProfile>()
                .Where(p => p.FreelancerProfilesId == contract.FreelancerProfilesId.Value)
                .Select(p => p.UserId)
                .FirstOrDefaultAsync(cancellationToken);

            return freelancerUser != Guid.Empty ? freelancerUser : null;
        }

        // Otherwise current user is the freelancer, notify client
        var clientUser = await _context.Set<ClientProfile>()
            .Where(p => p.ClientProfilesId == contract.ClientProfilesId)
            .Select(p => p.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        return clientUser != Guid.Empty ? clientUser : null;
    }

    private static DisputeResponse BuildDisputeResponse(
        Dispute dispute,
        DisputeEvidence? evidence,
        Guid initiatorId,
        string initiatorRole,
        Contract contract)
    {
        var evidences = new List<DisputeEvidenceResponse>();
        if (evidence is not null)
        {
            evidences.Add(new DisputeEvidenceResponse(
                evidence.DisputeEvidenceId,
                evidence.DisputesId,
                evidence.UploadedById,
                evidence.FileName,
                evidence.FileUrl,
                evidence.FileSize,
                evidence.Description,
                evidence.CreatedAt));
        }

        return new DisputeResponse(
            dispute.DisputesId,
            dispute.ContractsId,
            dispute.InitiatorId,
            null, // InitiatorName — not loaded from DB in create response
            initiatorRole,
            dispute.MilestonesId,
            null, // MilestoneTitle — not loaded from DB in create response
            dispute.Reason,
            dispute.Status,
            dispute.Resolution,
            null, // ResolutionLabel — not resolved yet
            dispute.ResolutionNote,
            dispute.ResolvedAt,
            dispute.CreatedAt,
            dispute.UpdatedAt,
            evidences);
    }
}
