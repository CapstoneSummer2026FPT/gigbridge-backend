using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Contracts.Common.DTOs;
using Application.Features.Contracts.Common.Internal;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Signing.Common.Sign.Commands;

public sealed class SignContractCommandHandler :
    IRequestHandler<SignContractCommand, ContractWorkflowResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IChatRealtimeNotifier _chatRealtimeNotifier;

    public SignContractCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IChatRealtimeNotifier chatRealtimeNotifier)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _chatRealtimeNotifier = chatRealtimeNotifier;
    }

    public async Task<ContractWorkflowResponse> Handle(
        SignContractCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Request.SignatureImageUrl))
        {
            throw new BadRequestException("Signature image URL is required.");
        }

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
        var document = await ContractEsignRenderer.EnsureDocumentAsync(_context, contract, now, cancellationToken);

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

        existingSignature.SignatureImageUrl = command.Request.SignatureImageUrl;
        existingSignature.SignatureWidth = command.Request.SignatureWidth;
        existingSignature.SignatureHeight = command.Request.SignatureHeight;
        existingSignature.Status = (int)ESignSignatureStatus.Signed;
        existingSignature.SignedAt = now;
        existingSignature.IpAddress = command.IpAddress;
        existingSignature.UserAgent = command.UserAgent;

        var signedRoles = await _context.Set<EsignSignature>()
            .Where(signature =>
                signature.EsignDocumentsId == document.EsignDocumentsId &&
                signature.Status == (int)ESignSignatureStatus.Signed)
            .Select(signature => signature.SignerRole)
            .ToListAsync(cancellationToken);

        if (!signedRoles.Contains((int)signerRole))
        {
            signedRoles.Add((int)signerRole);
        }

        if (signedRoles.Contains((int)ESignerRole.Client) &&
            signedRoles.Contains((int)ESignerRole.Freelancer))
        {
            document.Status = (int)ESignDocumentStatus.FullySigned;
            document.FinalizedAt = now;
            document.UpdatedAt = now;
            contract.Status = (int)ContractStatus.PendingEscrow;
            contract.UpdatedAt = now;

            var conversations = await _context.Set<Conversation>()
                .Where(conversation => conversation.ContractsId == contract.ContractsId)
                .ToListAsync(cancellationToken);

            foreach (var conversation in conversations)
            {
                if (conversation.ConversationType == (int)ConversationType.JobNegotiation)
                {
                    conversation.ConversationType = (int)ConversationType.ContractWorkroom;
                    ContractConversationEvents.AddSystemMessage(
                        _context,
                        conversation,
                        "Contract fully signed. Workroom is ready while escrow awaits funding.",
                        now);
                }
            }
        }
        else
        {
            document.Status = (int)ESignDocumentStatus.PartiallySigned;
            document.UpdatedAt = now;
        }

        await _context.SaveChangesAsync(cancellationToken);

        if (contract.Status == (int)ContractStatus.PendingEscrow)
        {
            var conversationIds = await _context.Set<Conversation>()
                .Where(conversation => conversation.ContractsId == contract.ContractsId)
                .Select(conversation => conversation.ConversationsId)
                .ToListAsync(cancellationToken);

            foreach (var conversationId in conversationIds)
            {
                await _chatRealtimeNotifier.SendConversationEventAsync(
                    conversationId,
                    "ContractFullySigned",
                    new { contractId = contract.ContractsId },
                    cancellationToken);
            }
        }

        return new ContractWorkflowResponse(
            contract.ContractsId,
            contract.Status,
            null,
            document.EsignDocumentsId);
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
