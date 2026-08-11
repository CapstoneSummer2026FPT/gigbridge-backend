using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Common.Services;
using Application.Features.Contracts.Common.DTOs;
using Application.Features.Contracts.Common.Internal;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Application.Features.Contracts.Signing.Common.Sign.Commands;

public sealed class SignContractCommandHandler :
    IRequestHandler<SignContractCommand, ContractWorkflowResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IChatRealtimeNotifier _chatRealtimeNotifier;
    private readonly IMediaService _mediaService;
    private readonly IContractEsignDocumentGenerator _documentGenerator;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public SignContractCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IChatRealtimeNotifier chatRealtimeNotifier,
        IMediaService mediaService,
        IContractEsignDocumentGenerator documentGenerator)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _chatRealtimeNotifier = chatRealtimeNotifier;
        _mediaService = mediaService;
        _documentGenerator = documentGenerator;
    }

    public async Task<ContractWorkflowResponse> Handle(
        SignContractCommand command,
        CancellationToken cancellationToken)
    {
        var contract = await _context.Set<Contract>()
            .FirstOrDefaultAsync(contract => contract.ContractsId == command.ContractId, cancellationToken);

        if (contract is null)
        {
            throw new NotFoundException("Contract does not exist.");
        }

        if (contract.Status != (int)ContractStatus.PendingSignature)
        {
            throw new BadRequestException("Contract can only be signed after details are confirmed.");
        }

        var signerRole = await ResolveSignerRoleAsync(contract, command.UserId, cancellationToken);
        var now = _dateTimeService.UtcNow;
        var document = await ContractEsignRenderer.EnsureDocumentAsync(
            _context, _documentGenerator, contract, now, cancellationToken);
        var snapshot = ContractEsignRenderer.GetSnapshot(document);

        var existingSignature = await _context.Set<EsignSignature>()
            .FirstOrDefaultAsync(
                signature =>
                    signature.EsignDocumentsId == document.EsignDocumentsId &&
                    signature.UserId == command.UserId,
                cancellationToken);

        if (existingSignature is not null &&
            existingSignature.Status == (int)ESignSignatureStatus.Signed)
        {
            throw new ConflictException("This user has already signed the contract.");
        }

        if (existingSignature is null)
        {
            existingSignature = new EsignSignature
            {
                EsignSignaturesId = Guid.NewGuid(),
                EsignDocumentsId = document.EsignDocumentsId,
                UserId = command.UserId,
                SignerRole = (int)signerRole,
                CreatedAt = now
            };
            _context.Set<EsignSignature>().Add(existingSignature);
        }

        existingSignature.SignatureImageUrl = await SignatureImageStorage.UploadSignatureImageAsync(
            _mediaService,
            command.Request.SignatureImageUrl,
            document.EsignDocumentsId,
            command.UserId,
            cancellationToken);
        existingSignature.SignatureWidth = command.Request.SignatureWidth;
        existingSignature.SignatureHeight = command.Request.SignatureHeight;
        existingSignature.Status = (int)ESignSignatureStatus.Signed;
        existingSignature.SignedAt = now;
        existingSignature.IpAddress = command.IpAddress;
        existingSignature.UserAgent = command.UserAgent;
        existingSignature.PolicyVersion = snapshot.PolicyVersion;
        existingSignature.PolicyAcceptedAt = now;

        var readiness = await ContractEsignSignatureBridge.ApplyClientJobPostSignatureAndGetReadinessAsync(
            _context,
            contract,
            document,
            now,
            cancellationToken,
            signerRole);

        var isFullySigned = readiness.IsFullySigned;

        Guid? escrowId = null;

        if (isFullySigned)
        {
            var signed = await _context.Set<EsignSignature>()
                .Where(signature =>
                    signature.EsignDocumentsId == document.EsignDocumentsId &&
                    signature.Status == (int)ESignSignatureStatus.Signed)
                .ToListAsync(cancellationToken);
            if (signed.All(signature => signature.UserId != existingSignature.UserId))
            {
                signed.Add(existingSignature);
            }

            var clientSignature = ContractEsignRenderer.ToSignatureSnapshot(
                signed.Single(signature => signature.SignerRole == (int)ESignerRole.Client));
            var freelancerSignature = ContractEsignRenderer.ToSignatureSnapshot(
                signed.Single(signature => signature.SignerRole == (int)ESignerRole.Freelancer));
            var documentHash = ContractEsignRenderer.ComputeFinalHash(document, clientSignature, freelancerSignature);
            var finalized = await _documentGenerator.GenerateAsync(
                snapshot,
                clientSignature,
                freelancerSignature,
                documentHash,
                cancellationToken);

            document.ContractSnapshotJson ??= JsonSerializer.Serialize(snapshot, JsonOptions);
            document.DocumentHash = documentHash;
            document.FinalizedDocumentContent = finalized.Content;
            document.FinalizedDocumentFileName = finalized.FileName;
            document.FinalizedDocumentMimeType = finalized.MimeType;
            document.FinalizedDocumentSizeBytes = finalized.Content.LongLength;
            document.Status = (int)ESignDocumentStatus.FullySigned;
            document.FinalizedAt = now;
            document.UpdatedAt = now;

            AddFinalContractEmail(snapshot, document.EsignDocumentsId, snapshot.Client, now);
            AddFinalContractEmail(snapshot, document.EsignDocumentsId, snapshot.Freelancer, now);

            var escrow = await ContractEscrowReadiness.EnsurePendingEscrowAsync(
                _context,
                contract,
                now,
                cancellationToken);
            escrowId = escrow.ContractEscrowId;

            var conversations = await _context.Set<Conversation>()
                .Where(conversation => conversation.ContractsId == contract.ContractsId)
                .ToListAsync(cancellationToken);

            foreach (var conversation in conversations)
            {
                ContractConversationEvents.AddSystemMessage(
                    _context,
                    conversation,
                    "Contract fully signed. Waiting for client escrow funding.",
                    now);
            }
        }
        else
        {
            document.Status = (int)ESignDocumentStatus.PartiallySigned;
            document.UpdatedAt = now;
        }

        await _context.SaveChangesAsync(cancellationToken);

        if (isFullySigned)
        {
            var participantUserIds = await _context.Set<ConversationParticipant>()
                .AsNoTracking()
                .Where(participant =>
                    participant.Conversations.ContractsId == contract.ContractsId &&
                    participant.LeftAt == null &&
                    participant.DeletedAt == null)
                .Select(participant => participant.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (participantUserIds.Any())
            {
                await _chatRealtimeNotifier.SendUsersEventAsync(
                    [.. participantUserIds],
                    "ContractReadyForEscrowFunding",
                    new { contractId = contract.ContractsId },
                    cancellationToken);
            }
        }

        return new ContractWorkflowResponse(
            contract.ContractsId,
            contract.Status,
            escrowId,
            document.EsignDocumentsId);
    }

    private void AddFinalContractEmail(
        ContractDocumentSnapshot snapshot,
        Guid documentId,
        ContractPartySnapshot recipient,
        DateTime now)
    {
        var deliveryKey = $"esign:{documentId:D}:final:{recipient.UserId:D}";
        var payload = new ContractEsignDeliveryPayload(
            documentId,
            recipient.Email,
            recipient.FullName,
            snapshot.ProjectTitle);
        _context.Set<DeliveryOutbox>().Add(new DeliveryOutbox
        {
            DeliveryOutboxId = Guid.NewGuid(),
            DeliveryKey = deliveryKey,
            ScheduleId = null,
            RecipientUserId = recipient.UserId,
            EventSequence = snapshot.ContractVersion,
            Channel = (int)DeliveryChannel.Email,
            Payload = JsonSerializer.Serialize(payload, JsonOptions),
            Status = (int)DeliveryOutboxStatus.Pending,
            NextAttemptAt = now,
            CreatedAt = now
        });
    }

    private async Task<ESignerRole> ResolveSignerRoleAsync(
        Contract contract,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var clientProfile = await _context.Set<ClientProfile>()
            .FirstOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);

        if (clientProfile is not null &&
            clientProfile.ClientProfilesId == contract.ClientProfilesId)
        {
            return ESignerRole.Client;
        }

        var freelancerProfile = await ContractParticipantGuard.EnsureFreelancerAsync(
            _context,
            contract,
            userId,
            cancellationToken);

        return freelancerProfile.FreelancerProfilesId == contract.FreelancerProfilesId
            ? ESignerRole.Freelancer
            : throw new ForbiddenAccessException("Only contract participants can sign.");
    }
}
