using Application.Common.InternalServices.ESign.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.InternalServices.ESign.Interfaces;
using Application.Features.Contracts.Common.DTOs;
using Domain.Entities;
using Domain.Enums.ESign;
using Microsoft.EntityFrameworkCore;

namespace Application.Common.InternalServices.ESign.Services;
internal static class ContractEsignRenderer
{
    public const string FixedPriceTemplateCode = "CONTRACT_FIXED_PRICE";
    public const string PolicyVersion = "Ver 1.0 Gigbridge";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<(EsignDocument Document, EsignDocumentContent Content)> EnsureDocumentAsync(
        IApplicationDbContext context,
        IContractEsignDocumentGenerator documentGenerator,
        Contract contract,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var existing = await context.Set<EsignDocument>()
            .Where(document =>
                document.ContractsId == contract.ContractsId &&
                document.Status != (int)ESignDocumentStatus.Voided &&
                document.Status != (int)ESignDocumentStatus.Expired)
            .OrderByDescending(document => document.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var existingContent = existing is null
            ? null
            : await context.Set<EsignDocumentContent>()
                .FirstOrDefaultAsync(content => content.EsignDocumentsId == existing.EsignDocumentsId, cancellationToken);

        if (existing is not null && existingContent is not null &&
            (!string.IsNullOrWhiteSpace(existingContent.ContractSnapshotJson) ||
             existing.Status == (int)ESignDocumentStatus.FullySigned))
        {
            return (existing, existingContent);
        }

        var template = existing is null
            ? await context.Set<EsignTemplate>()
                .Where(item => item.TemplateCode == FixedPriceTemplateCode && item.IsActive)
                .OrderByDescending(item => item.Version)
                .ThenByDescending(item => item.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken)
            : await context.Set<EsignTemplate>()
                .FirstOrDefaultAsync(item => item.EsignTemplatesId == existing.EsignTemplatesId, cancellationToken);

        if (template is null)
        {
            throw new BadRequestException("Active e-sign contract template CONTRACT_FIXED_PRICE is not configured.");
        }

        var milestones = await context.Set<Milestone>()
            .Include(milestone => milestone.WorkItems)
            .Where(milestone => milestone.ContractsId == contract.ContractsId)
            .OrderBy(milestone => milestone.SortOrder)
            .ThenBy(milestone => milestone.CreatedAt)
            .ToListAsync(cancellationToken);

        var clientProfile = await context.Set<ClientProfile>()
            .FirstOrDefaultAsync(profile => profile.ClientProfilesId == contract.ClientProfilesId, cancellationToken);
        var freelancerProfile = contract.FreelancerProfilesId.HasValue
            ? await context.Set<FreelancerProfile>()
                .FirstOrDefaultAsync(
                    profile => profile.FreelancerProfilesId == contract.FreelancerProfilesId.Value,
                    cancellationToken)
            : null;
        var clientUser = clientProfile is null
            ? null
            : await context.Set<User>().FirstOrDefaultAsync(item => item.UserId == clientProfile.UserId, cancellationToken);
        var freelancerUser = freelancerProfile is null
            ? null
            : await context.Set<User>().FirstOrDefaultAsync(item => item.UserId == freelancerProfile.UserId, cancellationToken);

        if (clientProfile is null || freelancerProfile is null || clientUser is null || freelancerUser is null)
        {
            throw new BadRequestException("Both contract participants must exist before creating an ESign document.");
        }

        var proposal = contract.ProposalsId.HasValue
            ? await context.Set<Proposal>().AsNoTracking()
                .FirstOrDefaultAsync(item => item.ProposalsId == contract.ProposalsId.Value, cancellationToken)
            : null;
        var finalOffer = await context.Set<NegotiationOffer>().AsNoTracking()
            .Where(item => item.ContractsId == contract.ContractsId)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var documentId = existing?.EsignDocumentsId ?? Guid.NewGuid();
        var documentCode = existing?.DocumentCode ?? $"GB-{now:yyyyMMdd}-{Guid.NewGuid():N}"[..22].ToUpperInvariant();
        var snapshot = BuildSnapshot(
            documentId,
            documentCode,
            template.Version,
            contract,
            clientProfile,
            clientUser,
            freelancerProfile,
            freelancerUser,
            proposal,
            finalOffer,
            milestones,
            existing?.CreatedAt ?? now,
            existing?.DocumentHash);
        var snapshotJson = JsonSerializer.Serialize(snapshot, JsonOptions);
        var preview = documentGenerator.RenderPreview(snapshot);
        var snapshotHash = Hash(snapshotJson);

        if (existing is not null)
        {
            var content = existingContent ?? new EsignDocumentContent { EsignDocumentsId = existing.EsignDocumentsId };
            if (existingContent is null)
            {
                context.Set<EsignDocumentContent>().Add(content);
            }

            content.ContractSnapshotJson = snapshotJson;
            content.RenderedHtmlContent = preview;
            existing.DocumentHash = snapshotHash;
            existing.UpdatedAt = now;
            existing.ContentRevision++;
            return (existing, content);
        }

        var document = new EsignDocument
        {
            EsignDocumentsId = documentId,
            EsignTemplatesId = template.EsignTemplatesId,
            JobPostsId = contract.JobPostsId,
            ContractsId = contract.ContractsId,
            DocumentCode = documentCode,
            Status = (int)ESignDocumentStatus.PendingSignatures,
            DocumentHash = snapshotHash,
            ContentRevision = 1,
            CreatedAt = now
        };
        var newContent = new EsignDocumentContent
        {
            EsignDocumentsId = documentId,
            RenderedHtmlContent = preview,
            ContractSnapshotJson = snapshotJson
        };

        context.Set<EsignDocument>().Add(document);
        context.Set<EsignDocumentContent>().Add(newContent);
        return (document, newContent);
    }

    public static ContractDocumentSnapshot GetSnapshot(EsignDocumentContent content)
    {
        if (string.IsNullOrWhiteSpace(content.ContractSnapshotJson))
        {
            throw new BadRequestException("The ESign contract snapshot is missing.");
        }

        return JsonSerializer.Deserialize<ContractDocumentSnapshot>(content.ContractSnapshotJson, JsonOptions)
            ?? throw new BadRequestException("The ESign contract snapshot is invalid.");
    }

    public static ContractSignatureSnapshot ToSignatureSnapshot(EsignSignature signature)
    {
        if (!signature.SignedAt.HasValue || string.IsNullOrWhiteSpace(signature.SignatureImageUrl))
        {
            throw new BadRequestException("A completed ESign signature is missing required evidence.");
        }

        return new ContractSignatureSnapshot(
            signature.UserId,
            signature.SignerRole,
            signature.SignatureImageUrl,
            signature.SignatureWidth,
            signature.SignatureHeight,
            signature.SignedAt.Value,
            signature.IpAddress,
            signature.UserAgent,
            signature.PolicyVersion,
            signature.PolicyAcceptedAt,
            true);
    }

    public static ContractSignatureSnapshot ToDraftSignatureSnapshot(EsignSignature signature)
    {
        if (!signature.DraftSubmittedAt.HasValue || string.IsNullOrWhiteSpace(signature.SignatureImageUrl))
        {
            throw new BadRequestException("A contract signature draft is missing required evidence.");
        }

        return new ContractSignatureSnapshot(
            signature.UserId,
            signature.SignerRole,
            signature.SignatureImageUrl,
            signature.SignatureWidth,
            signature.SignatureHeight,
            signature.DraftSubmittedAt.Value,
            signature.IpAddress,
            signature.UserAgent,
            signature.PolicyVersion,
            signature.PolicyAcceptedAt,
            false);
    }

    public static ContractDocumentSnapshot WithIdentityCodes(
        ContractDocumentSnapshot snapshot,
        string? clientIdentityOrTaxCode,
        string? freelancerIdentityOrTaxCode) =>
        snapshot with
        {
            Client = snapshot.Client with { IdentityOrTaxCode = clientIdentityOrTaxCode },
            Freelancer = snapshot.Freelancer with { IdentityOrTaxCode = freelancerIdentityOrTaxCode }
        };

    public static string ComputeFinalHash(
        EsignDocumentContent content,
        ContractSignatureSnapshot clientSignature,
        ContractSignatureSnapshot freelancerSignature) =>
        Hash($"{JsonSerializer.Serialize(GetSnapshot(content), JsonOptions)}\n{JsonSerializer.Serialize(clientSignature, JsonOptions)}\n{JsonSerializer.Serialize(freelancerSignature, JsonOptions)}");

    private static ContractDocumentSnapshot BuildSnapshot(
        Guid documentId,
        string documentCode,
        int templateVersion,
        Contract contract,
        ClientProfile client,
        User clientUser,
        FreelancerProfile freelancer,
        User freelancerUser,
        Proposal? proposal,
        NegotiationOffer? finalOffer,
        IReadOnlyList<Milestone> milestones,
        DateTime createdAt,
        string? previousHash)
    {
        var scopeLines = milestones.SelectMany(milestone =>
            new[] { milestone.Title }
                .Concat(milestone.WorkItems.OrderBy(item => item.OrderIndex).Select(item => item.Title)));
        var scope = string.Join("; ", scopeLines.Where(value => !string.IsNullOrWhiteSpace(value)));
        if (!string.IsNullOrWhiteSpace(finalOffer?.ScopeSummary))
        {
            scope = string.IsNullOrWhiteSpace(scope) ? finalOffer.ScopeSummary : $"{finalOffer.ScopeSummary}; {scope}";
        }

        var milestoneSnapshots = milestones.Select((milestone, index) =>
        {
            var deliverables = new[] { milestone.Deliverables }
                .Concat(milestone.WorkItems.OrderBy(item => item.OrderIndex).Select(item => item.Deliverables))
                .Where(value => !string.IsNullOrWhiteSpace(value));
            return new ContractMilestoneSnapshot(
                index + 1,
                milestone.Title,
                milestone.Description,
                string.Join("; ", deliverables),
                milestone.AcceptanceCriteria,
                index == 0 ? contract.StartDate : milestones[index - 1].DueDate,
                milestone.DueDate,
                null,
                milestone.Amount);
        }).ToList();

        return new ContractDocumentSnapshot(
            documentId,
            contract.ContractsId,
            contract.JobPostsId,
            contract.ProposalsId,
            finalOffer?.NegotiationOfferId,
            documentCode,
            Math.Max(1, contract.RevisionNumber),
            templateVersion,
            PolicyVersion,
            createdAt,
            contract.StartDate,
            contract.EndDate,
            contract.Title,
            contract.Description,
            scope,
            proposal?.OutOfScope,
            contract.TotalBudget,
            new ContractPartySnapshot(
                clientUser.UserId,
                client.ClientProfilesId,
                clientUser.FullName,
                clientUser.Email,
                clientUser.PhoneNumber,
                client.Location,
                Representative: clientUser.FullName,
                RepresentativeTitle: client.CompanyName),
            new ContractPartySnapshot(
                freelancerUser.UserId,
                freelancer.FreelancerProfilesId,
                freelancerUser.FullName,
                freelancerUser.Email,
                freelancerUser.PhoneNumber,
                freelancer.Location),
            milestoneSnapshots,
            previousHash);
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
