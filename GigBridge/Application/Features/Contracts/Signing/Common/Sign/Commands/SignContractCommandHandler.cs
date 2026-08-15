using Application.Common.InternalServices.Contracts.Services;
using Application.Common.InternalServices.ESign.Models;
using Domain.Enums.Chat;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Documents;
using Application.Common.InternalServices.Auditing.Interfaces;
using Application.Common.Interfaces.Media;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Chat.Interfaces;
using Application.Common.InternalServices.ESign.Services;
using Application.Features.Contracts.Common.DTOs;
using Application.Features.Contracts.Common.Internal;
using Application.Features.ESign.Common.Internal;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Auditing;
using Domain.Enums.Contracts;
using Domain.Enums.ESign;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Application.Common.InternalServices.ESign.Interfaces;
using Application.Common.Interfaces.Caching;
using Application.Common.InternalServices.Auth.Services;

namespace Application.Features.Contracts.Signing.Common.Sign.Commands;

public sealed class SignContractCommandHandler :
    IRequestHandler<SignContractCommand, ContractWorkflowResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IChatRealtimeNotifier _chatRealtimeNotifier;
    private readonly IMediaService _mediaService;
    private readonly IContractEsignDocumentGenerator _documentGenerator;
    private readonly IWordToPdfConverter _pdfConverter;
    private readonly IUserAuditLogService _userAuditLog;
    private readonly ICacheService _cacheService;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public SignContractCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IChatRealtimeNotifier chatRealtimeNotifier,
        IMediaService mediaService,
        IContractEsignDocumentGenerator documentGenerator,
        IWordToPdfConverter pdfConverter,
        IUserAuditLogService userAuditLog,
        ICacheService cacheService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _chatRealtimeNotifier = chatRealtimeNotifier;
        _mediaService = mediaService;
        _documentGenerator = documentGenerator;
        _pdfConverter = pdfConverter;
        _userAuditLog = userAuditLog;
        _cacheService = cacheService;
    }

    public async Task<ContractWorkflowResponse> Handle(
        SignContractCommand command,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
        await transaction.AcquireTransactionLockAsync(
            ContractEscrowLock.ForContract(command.ContractId), cancellationToken);

        var contract = await _context.Set<Contract>()
            .FirstOrDefaultAsync(contract => contract.ContractsId == command.ContractId, cancellationToken);

        if (contract is null)
        {
            throw new NotFoundException("Contract does not exist.");
        }

        if (contract.Status != (int)ContractStatus.PendingSignature)
        {
            if (contract.Status is (int)ContractStatus.PendingEscrow or
                (int)ContractStatus.Active or
                (int)ContractStatus.Completed)
            {
                throw new ConflictException("This contract has already been fully signed.");
            }

            throw new BadRequestException("Contract can only be signed after details are confirmed.");
        }

        var signerRole = await ResolveSignerRoleAsync(contract, command.UserId, cancellationToken);
        var now = _dateTimeService.UtcNow;
        var document = await ContractEsignRenderer.EnsureDocumentAsync(
            _context, _documentGenerator, contract, now, cancellationToken);
        var snapshot = ContractEsignRenderer.GetSnapshot(document);

        if (!command.Request.PolicyAccepted ||
            !string.Equals(command.Request.PolicyVersion, snapshot.PolicyVersion, StringComparison.Ordinal))
        {
            throw new BadRequestException("The current E-sign policy must be accepted before submitting a signature draft.");
        }

        var signer = await _context.Set<User>()
            .FirstOrDefaultAsync(user => user.UserId == command.UserId, cancellationToken)
            ?? throw new NotFoundException("Signer user does not exist.");
        var submittedIdentityCode = ContractIdentityCode.Normalize(
            command.Request.IdentityOrTaxCode);
        var hasStoredIdentityCode = ContractIdentityCode.IsValid(signer.IdentityOrTaxCode);
        var identityOrTaxCode = hasStoredIdentityCode
            ? ContractIdentityCode.Normalize(signer.IdentityOrTaxCode)
            : submittedIdentityCode;

        var counterpartParty = signerRole == ESignerRole.Client
            ? snapshot.Freelancer
            : snapshot.Client;
        var documentSignatures = await _context.Set<EsignSignature>()
            .Where(signature => signature.EsignDocumentsId == document.EsignDocumentsId)
            .ToListAsync(cancellationToken);
        var existingSignature = documentSignatures.SingleOrDefault(signature =>
            signature.UserId == command.UserId);

        if (existingSignature is not null &&
            existingSignature.Status == (int)ESignSignatureStatus.Signed)
        {
            throw new ConflictException("This user has already signed the contract.");
        }

        var counterpartSignatureIdentityCode = documentSignatures
            .SingleOrDefault(signature =>
                signature.UserId == counterpartParty.UserId &&
                signature.Status is (int)ESignSignatureStatus.Pending or
                    (int)ESignSignatureStatus.Signed)
            ?.IdentityOrTaxCode;
        var counterpartStoredIdentityCode = await _context.Set<User>()
            .AsNoTracking()
            .Where(user => user.UserId == counterpartParty.UserId)
            .Select(user => user.IdentityOrTaxCode)
            .SingleOrDefaultAsync(cancellationToken);

        ContractIdentityCode.EnsureDifferentParties(
            identityOrTaxCode,
            counterpartSignatureIdentityCode,
            counterpartStoredIdentityCode,
            counterpartParty.IdentityOrTaxCode);

        if (!hasStoredIdentityCode)
        {
            await ConsumeIdentityVerificationAsync(
                signer.Email,
                identityOrTaxCode,
                command.Request.IdentityVerificationTicket,
                cancellationToken);
            signer.IdentityOrTaxCode = identityOrTaxCode;
            signer.UpdatedAt = now;
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

        if (!string.IsNullOrWhiteSpace(command.Request.SignatureImageUrl))
        {
            existingSignature.SignatureImageUrl = await SignatureImageStorage.UploadSignatureImageAsync(
                _mediaService,
                command.Request.SignatureImageUrl,
                document.EsignDocumentsId,
                command.UserId,
                cancellationToken);
            existingSignature.SignatureWidth = command.Request.SignatureWidth;
            existingSignature.SignatureHeight = command.Request.SignatureHeight;
        }
        else if (string.IsNullOrWhiteSpace(existingSignature.SignatureImageUrl))
        {
            throw new BadRequestException("Signature image is required when creating the first signature draft.");
        }

        existingSignature.Status = (int)ESignSignatureStatus.Pending;
        existingSignature.SignedAt = null;
        existingSignature.IdentityOrTaxCode = identityOrTaxCode;
        existingSignature.DraftSubmittedAt = now;
        existingSignature.UpdatedAt = now;
        existingSignature.IpAddress = command.IpAddress;
        existingSignature.UserAgent = command.UserAgent;
        existingSignature.PolicyVersion = command.Request.PolicyVersion;
        existingSignature.PolicyAcceptedAt = now;
        existingSignature.DeclinedAt = null;
        existingSignature.DeclineReason = null;

        document.Status = (int)ESignDocumentStatus.PendingSignatures;
        document.UpdatedAt = now;
        ClearFinalArtifact(document);
        InvalidatePdfArtifact(document);

        await _context.SaveChangesAsync(cancellationToken);

        var drafts = await _context.Set<EsignSignature>()
            .Where(signature =>
                signature.EsignDocumentsId == document.EsignDocumentsId &&
                signature.Status == (int)ESignSignatureStatus.Pending)
            .OrderBy(signature => signature.SignerRole)
            .ToListAsync(cancellationToken);

        var clientDraft = drafts.FirstOrDefault(signature =>
            signature.SignerRole == (int)ESignerRole.Client && IsValidDraft(signature, snapshot.PolicyVersion));
        var freelancerDraft = drafts.FirstOrDefault(signature =>
            signature.SignerRole == (int)ESignerRole.Freelancer && IsValidDraft(signature, snapshot.PolicyVersion));
        var isFullySigned = clientDraft is not null && freelancerDraft is not null;

        Guid? escrowId = null;

        if (isFullySigned)
        {
            var finalSnapshot = ContractEsignRenderer.WithIdentityCodes(
                snapshot,
                clientDraft!.IdentityOrTaxCode,
                freelancerDraft!.IdentityOrTaxCode);
            document.ContractSnapshotJson = JsonSerializer.Serialize(finalSnapshot, JsonOptions);

            foreach (var signature in new[] { clientDraft, freelancerDraft })
            {
                signature.Status = (int)ESignSignatureStatus.Signed;
                signature.SignedAt = signature.DraftSubmittedAt;
                signature.UpdatedAt = now;
            }

            var clientSignature = ContractEsignRenderer.ToSignatureSnapshot(
                clientDraft);
            var freelancerSignature = ContractEsignRenderer.ToSignatureSnapshot(
                freelancerDraft);
            var documentHash = ContractEsignRenderer.ComputeFinalHash(document, clientSignature, freelancerSignature);
            var finalized = await _documentGenerator.GenerateAsync(
                finalSnapshot,
                clientSignature,
                freelancerSignature,
                documentHash,
                cancellationToken);
            var finalizedPdf = await _pdfConverter.ConvertAsync(
                finalized.Content,
                finalized.FileName,
                cancellationToken);

            document.DocumentHash = documentHash;
            document.FinalizedDocumentContent = finalized.Content;
            document.FinalizedDocumentFileName = finalized.FileName;
            document.FinalizedDocumentMimeType = finalized.MimeType;
            document.FinalizedDocumentSizeBytes = finalized.Content.LongLength;
            document.Status = (int)ESignDocumentStatus.FullySigned;
            document.FinalizedAt = now;
            document.UpdatedAt = now;
            document.PdfDocumentContent = finalizedPdf;
            document.PdfDocumentFileName = ESignPdfArtifactRevision.ContractFileName;
            document.PdfDocumentHash = $"{documentHash}{ESignPdfArtifactRevision.ContractTemplate}";
            document.PdfSignatureCount = 2;

            AddFinalContractEmail(finalSnapshot, document.EsignDocumentsId, finalSnapshot.Client, now);
            AddFinalContractEmail(finalSnapshot, document.EsignDocumentsId, finalSnapshot.Freelancer, now);

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

            foreach (var signature in new[] { clientDraft, freelancerDraft })
            {
                _userAuditLog.Add(
                    signature.UserId,
                    signature.SignerRole == (int)ESignerRole.Client ? UserRole.Client : UserRole.Freelancer,
                    AuditUserActionType.SignedEsignContract,
                    contract.ContractsId,
                    "Contract signature finalized after both parties submitted valid signature drafts.",
                    relatedEntityId: document.EsignDocumentsId,
                    relatedEntityType: nameof(EsignDocument));
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

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

    private async Task ConsumeIdentityVerificationAsync(
        string email,
        string identityCode,
        string? verificationTicket,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(verificationTicket))
        {
            throw new BadRequestException(
                "Verify the identity code through your account email before signing.");
        }

        var verificationKey = OtpSecurity.VerifiedKey(
            OtpPurpose.IdentityVerification,
            EmailCanonicalizer.Canonicalize(email),
            verificationTicket,
            identityCode);
        var isVerified = await _cacheService.GetAndRemoveAsync<bool>(
            verificationKey,
            cancellationToken);
        if (!isVerified)
        {
            throw new BadRequestException(
                "Identity verification is invalid or has expired. Please verify your email again.");
        }
    }

    private static bool IsValidDraft(EsignSignature signature, string policyVersion) =>
        signature.DraftSubmittedAt.HasValue &&
        !string.IsNullOrWhiteSpace(signature.SignatureImageUrl) &&
        ContractIdentityCode.IsValid(signature.IdentityOrTaxCode) &&
        signature.PolicyAcceptedAt.HasValue &&
        string.Equals(signature.PolicyVersion, policyVersion, StringComparison.Ordinal);

    private static void InvalidatePdfArtifact(EsignDocument document)
    {
        document.PdfDocumentContent = null;
        document.PdfDocumentFileName = null;
        document.PdfDocumentHash = null;
        document.PdfSignatureCount = 0;
    }

    private static void ClearFinalArtifact(EsignDocument document)
    {
        document.FinalizedDocumentContent = null;
        document.FinalizedDocumentFileName = null;
        document.FinalizedDocumentMimeType = null;
        document.FinalizedDocumentSizeBytes = null;
        document.FinalizedAt = null;
        document.ExportedPdfUrl = null;
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
