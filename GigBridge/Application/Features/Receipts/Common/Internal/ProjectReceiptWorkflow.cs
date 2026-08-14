using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Contracts.Common.Internal;
using Application.Features.Receipts.Common.DTOs;
using Application.Features.Wallets.Common;
using Domain.Entities;
using Domain.Enums;
using Domain.Enums.Contracts;
using Domain.Enums.Contracts.Escrow;
using Domain.Enums.Contracts.Milestones;
using Domain.Enums.ESign;
using Domain.Enums.Wallets;
using Domain.Services.Payments;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Receipts.Common.Internal;

public static class ProjectReceiptWorkflow
{
    private const decimal BalanceTolerance = 0.01m;
    private const int CurrentTemplateVersion = 4;
    private const string PersonalCompanyName = "Null";
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<IReadOnlyList<ProjectReceipt>> EnsurePairAsync(
        IApplicationDbContext context,
        Guid contractId,
        DateTime issuedAt,
        CancellationToken cancellationToken)
    {
        var existing = await context.Set<ProjectReceipt>()
            .Where(receipt => receipt.ContractsId == contractId)
            .OrderBy(receipt => receipt.ReceiptType)
            .ToListAsync(cancellationToken);
        if (existing.Count == 2 && existing.All(item => item.TemplateVersion >= CurrentTemplateVersion))
        {
            return existing;
        }

        var contract = await context.Set<Contract>()
            .FirstOrDefaultAsync(item => item.ContractsId == contractId, cancellationToken)
            ?? throw new NotFoundException("Contract does not exist.");

        if (contract.Status != (int)ContractStatus.Completed || !contract.CompletedAt.HasValue)
        {
            throw new BadRequestException("Receipts are available only after the contract is completed.");
        }
        if (!contract.FreelancerProfilesId.HasValue)
        {
            throw new BadRequestException("The completed contract does not have a freelancer.");
        }

        var clientProfile = await context.Set<ClientProfile>()
            .FirstOrDefaultAsync(item => item.ClientProfilesId == contract.ClientProfilesId, cancellationToken)
            ?? throw new NotFoundException("Contract client profile does not exist.");
        var freelancerProfile = await context.Set<FreelancerProfile>()
            .FirstOrDefaultAsync(
                item => item.FreelancerProfilesId == contract.FreelancerProfilesId.Value,
                cancellationToken)
            ?? throw new NotFoundException("Contract freelancer profile does not exist.");
        var clientUser = await context.Set<User>()
            .FirstOrDefaultAsync(item => item.UserId == clientProfile.UserId, cancellationToken)
            ?? throw new NotFoundException("Contract client user does not exist.");
        var freelancerUser = await context.Set<User>()
            .FirstOrDefaultAsync(item => item.UserId == freelancerProfile.UserId, cancellationToken)
            ?? throw new NotFoundException("Contract freelancer user does not exist.");

        var escrow = await context.Set<ContractEscrow>()
            .FirstOrDefaultAsync(item => item.ContractsId == contractId, cancellationToken)
            ?? throw new NotFoundException("Contract escrow does not exist.");
        var remaining = Math.Max(0m, escrow.FundedAmount - escrow.ReleasedAmount);
        if (escrow.Status != (int)ContractEscrowStatus.Released || remaining >= BalanceTolerance)
        {
            throw new ConflictException("The completed contract escrow has not been fully released.");
        }

        var milestones = (await context.Set<Milestone>()
            .Where(item => item.ContractsId == contractId)
            .ToListAsync(cancellationToken))
            .OrderBy(item => item.SortOrder ?? int.MaxValue)
            .ThenBy(item => item.CreatedAt)
            .ToList();
        if (milestones.Count == 0 || milestones.Any(item => item.Status != (int)MilestoneStatus.Approved))
        {
            throw new ConflictException("The completed contract milestone ledger is incomplete.");
        }
        if (Math.Abs(escrow.RequiredAmount - escrow.FundedAmount) >= BalanceTolerance ||
            Math.Abs(milestones.Sum(item => item.Amount) - escrow.RequiredAmount) >= BalanceTolerance)
        {
            throw new ConflictException("The completed contract funding does not reconcile with its milestones.");
        }

        var projectStartedAt = milestones
            .Select(ResolveMilestoneStartedAt)
            .Min();

        var successfulReleaseLedger = await context.Set<EscrowTransaction>()
            .AsNoTracking()
            .Where(item =>
                item.ContractEscrowId == escrow.ContractEscrowId &&
                item.Type == (int)EscrowTransactionType.ReleaseToFreelancer &&
                item.Status == (int)EscrowTransactionStatus.Succeeded &&
                item.MilestonesId.HasValue)
            .ToListAsync(cancellationToken);
        // Transaction type/status are the source of truth. Release codes also include
        // admin and dispute variants, so filtering by a small set of prefixes would
        // incorrectly discard valid releases and make an otherwise completed contract
        // fail reconciliation.
        var releaseLedger = successfulReleaseLedger;
        if (Math.Abs(releaseLedger.Sum(item => item.Amount) - escrow.ReleasedAmount) >= BalanceTolerance)
        {
            throw new ConflictException("The escrow release ledger does not reconcile with the completed contract.");
        }
        foreach (var milestone in milestones)
        {
            var released = releaseLedger
                .Where(item => item.MilestonesId == milestone.MilestonesId)
                .Sum(item => item.Amount);
            if (Math.Abs(released - milestone.Amount) >= BalanceTolerance ||
                Math.Abs(milestone.ReleasedAmount - milestone.Amount) >= BalanceTolerance)
            {
                throw new ConflictException("The completed contract release ledger does not reconcile by milestone.");
            }
        }

        var walletTransactions = await context.Set<WalletTransaction>()
            .AsNoTracking()
            .Where(item =>
                item.ContractsId == contractId &&
                item.Status == (int)WalletTransactionStatus.Succeeded)
            .ToListAsync(cancellationToken);

        var clientFee = walletTransactions
            .Where(item =>
                item.UserId == clientUser.UserId &&
                item.Type == (int)WalletTransactionType.Adjustment &&
                item.IdempotencyKey != null &&
                item.IdempotencyKey.StartsWith(ServiceFeeWorkflow.ClientFundingFeePrefix))
            .Sum(item => item.TokenAmount);
        var acceptanceFee = walletTransactions
            .Where(item =>
                item.UserId == freelancerUser.UserId &&
                item.Type == (int)WalletTransactionType.Adjustment &&
                string.Equals(
                    item.IdempotencyKey,
                    $"{ServiceFeeWorkflow.AcceptJobFeePrefix}{contractId:N}",
                    StringComparison.Ordinal))
            .Sum(item => item.TokenAmount);
        // Retain legacy release fees so receipts for contracts created before the fee
        // moved to job acceptance still reflect the actual wallet ledger.
        var legacyReleaseFeeTransactions = walletTransactions
            .Where(item =>
                item.UserId == freelancerUser.UserId &&
                item.Type == (int)WalletTransactionType.Adjustment &&
                item.IdempotencyKey != null &&
                item.IdempotencyKey.StartsWith(ServiceFeeWorkflow.FreelancerReleaseFeePrefix) &&
                item.MilestonesId.HasValue)
            .ToList();
        var legacyReleaseFees = legacyReleaseFeeTransactions
            .GroupBy(item => item.MilestonesId!.Value)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.TokenAmount));
        var acceptanceFees = AllocateFeeByMilestone(milestones, acceptanceFee);
        var milestoneFees = milestones.ToDictionary(
            milestone => milestone.MilestonesId,
            milestone => acceptanceFees.GetValueOrDefault(milestone.MilestonesId) +
                legacyReleaseFees.GetValueOrDefault(milestone.MilestonesId));

        var finalReleases = releaseLedger.Where(IsFinalRelease).ToList();
        var earlyReleases = releaseLedger.Where(item => !IsFinalRelease(item)).ToList();
        var milestoneSnapshots = milestones.Select((milestone, index) =>
        {
            var early = earlyReleases
                .Where(item => item.MilestonesId == milestone.MilestonesId)
                .Sum(item => item.Amount);
            var final = finalReleases
                .Where(item => item.MilestonesId == milestone.MilestonesId)
                .Sum(item => item.Amount);
            var total = early + final;
            var fee = milestoneFees.GetValueOrDefault(milestone.MilestonesId);
            return new ProjectReceiptMilestoneSnapshot(
                index + 1,
                milestone.MilestonesId,
                milestone.Title,
                milestone.ApprovedAt,
                milestone.Amount,
                early,
                final,
                total,
                fee,
                total - fee,
                ResolveMilestoneStartedAt(milestone),
                milestone.DueDate);
        }).ToList();

        var contractCode = (await context.Set<EsignDocument>()
            .Where(item => item.ContractsId == contractId)
            .ToListAsync(cancellationToken))
            .Where(item => item.Status == (int)ESignDocumentStatus.FullySigned)
            .OrderByDescending(item => item.FinalizedAt ?? item.CreatedAt)
            .Select(item => item.DocumentCode)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(contractCode))
        {
            contractCode = $"GB-CTR-{contract.ContractsId:N}".ToUpperInvariant();
        }

        var finalReference = releaseLedger
            .OrderByDescending(item => IsFinalRelease(item))
            .ThenByDescending(item => item.CompletedAt ?? item.CreatedAt)
            .Select(item => item.GatewayTransactionCode)
            .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item))
            ?? "Không có";
        var totalEarly = earlyReleases.Sum(item => item.Amount);
        var totalFinal = finalReleases.Sum(item => item.Amount);
        var totalReleased = releaseLedger.Sum(item => item.Amount);
        var freelancerFee = acceptanceFee + legacyReleaseFees.Values.Sum();
        var finalFeeKeys = finalReleases
            .Where(item => !string.IsNullOrWhiteSpace(item.GatewayTransactionCode))
            .Select(item => $"{ServiceFeeWorkflow.FreelancerReleaseFeePrefix}{item.GatewayTransactionCode}")
            .ToHashSet(StringComparer.Ordinal);
        var legacyFinalFee = legacyReleaseFeeTransactions
            .Where(item => item.IdempotencyKey != null && finalFeeKeys.Contains(item.IdempotencyKey))
            .Sum(item => item.TokenAmount);
        var acceptanceFinalFee = milestones.Sum(milestone =>
        {
            var total = releaseLedger
                .Where(item => item.MilestonesId == milestone.MilestonesId)
                .Sum(item => item.Amount);
            var final = finalReleases
                .Where(item => item.MilestonesId == milestone.MilestonesId)
                .Sum(item => item.Amount);
            var fee = acceptanceFees.GetValueOrDefault(milestone.MilestonesId);
            return total <= 0m || final <= 0m
                ? 0m
                : final >= total
                    ? fee
                    : decimal.Round(fee * final / total, 4, MidpointRounding.AwayFromZero);
        });
        var finalFee = legacyFinalFee + acceptanceFinalFee;
        var earlyFee = freelancerFee - finalFee;

        var clientIdentityCode = await ResolveIdentityCodeAsync(
            context, contractId, clientUser, cancellationToken);
        var freelancerIdentityCode = await ResolveIdentityCodeAsync(
            context, contractId, freelancerUser, cancellationToken);

        var clientParty = new ProjectReceiptPartySnapshot(
            clientUser.UserId,
            clientUser.FullName,
            ValueOrPersonal(clientProfile.CompanyName),
            clientUser.Email,
            clientIdentityCode);
        var freelancerParty = new ProjectReceiptPartySnapshot(
            freelancerUser.UserId,
            freelancerUser.FullName,
            PersonalCompanyName,
            freelancerUser.Email,
            freelancerIdentityCode);

        var created = new List<ProjectReceipt>(2);
        foreach (var receiptType in new[] { ProjectReceiptType.Client, ProjectReceiptType.Freelancer })
        {
            var existingReceipt = existing.FirstOrDefault(item => item.ReceiptType == (int)receiptType);
            var receiptId = existingReceipt?.ProjectReceiptId ?? Guid.NewGuid();
            var effectiveIssuedAt = existingReceipt?.IssuedAt ?? issuedAt;
            var receiptNumber = existingReceipt?.ReceiptNumber
                ?? BuildReceiptNumber(receiptId, receiptType, effectiveIssuedAt);
            var snapshot = new ProjectReceiptSnapshot(
                receiptId,
                receiptNumber,
                (int)receiptType,
                effectiveIssuedAt,
                "Hoàn tất / Completed",
                clientParty,
                freelancerParty,
                contractCode,
                contract.ContractsId,
                contract.Title,
                contract.StartDate,
                contract.EndDate,
                contract.CompletedAt.Value,
                escrow.RequiredAmount,
                clientFee,
                escrow.RequiredAmount + clientFee,
                totalEarly,
                totalFinal,
                totalReleased,
                remaining,
                freelancerFee,
                totalReleased,
                totalEarly - earlyFee,
                totalFinal - finalFee,
                totalReleased - freelancerFee,
                finalReference,
                TokenWalletRules.VndPerToken,
                milestoneSnapshots,
                projectStartedAt);
            var json = JsonSerializer.Serialize(snapshot, SnapshotJsonOptions);
            var now = DateTime.UtcNow;

            if (existingReceipt is not null)
            {
                if (existingReceipt.TemplateVersion < CurrentTemplateVersion)
                {
                    existingReceipt.SnapshotJson = json;
                    existingReceipt.SnapshotHashSha256 = ComputeSha256(json);
                    QueueTemplateRegeneration(existingReceipt, now);
                }

                continue;
            }

            var receipt = new ProjectReceipt
            {
                ProjectReceiptId = receiptId,
                ContractsId = contract.ContractsId,
                OwnerUserId = receiptType == ProjectReceiptType.Client
                    ? clientUser.UserId
                    : freelancerUser.UserId,
                ReceiptType = (int)receiptType,
                ReceiptNumber = receiptNumber,
                TemplateVersion = CurrentTemplateVersion,
                IssuedAt = issuedAt,
                SnapshotJson = json,
                SnapshotHashSha256 = ComputeSha256(json),
                GenerationStatus = (int)ProjectReceiptGenerationStatus.Pending,
                NextGenerationAttemptAt = now,
                NextNotificationAttemptAt = now,
                EmailStatus = (int)ProjectReceiptEmailStatus.Pending,
                NextEmailAttemptAt = now,
                CreatedAt = now
            };
            context.Set<ProjectReceipt>().Add(receipt);
            created.Add(receipt);
        }

        return existing.Concat(created).OrderBy(item => item.ReceiptType).ToList();
    }

    public static ProjectReceiptSnapshot DeserializeSnapshot(ProjectReceipt receipt) =>
        JsonSerializer.Deserialize<ProjectReceiptSnapshot>(receipt.SnapshotJson, SnapshotJsonOptions)
        ?? throw new InvalidOperationException($"Receipt {receipt.ProjectReceiptId} has an invalid snapshot.");

    private static bool IsFinalRelease(EscrowTransaction transaction) =>
        transaction.GatewayTransactionCode?.StartsWith("ESCROW-FINAL-", StringComparison.Ordinal) == true;

    private static DateTime? ResolveMilestoneStartedAt(Milestone milestone) =>
        milestone.StartedAt ?? milestone.SubmittedAt ?? milestone.ApprovedAt;

    private static string ValueOrPersonal(string? value) =>
        string.IsNullOrWhiteSpace(value) ? PersonalCompanyName : value.Trim();

    private static IReadOnlyDictionary<Guid, decimal> AllocateFeeByMilestone(
        IReadOnlyList<Milestone> milestones,
        decimal totalFee)
    {
        var allocations = milestones.ToDictionary(item => item.MilestonesId, _ => 0m);
        var totalAmount = milestones.Sum(item => item.Amount);
        if (totalFee <= 0m || totalAmount <= 0m || milestones.Count == 0)
        {
            return allocations;
        }

        var remainingFee = totalFee;
        for (var index = 0; index < milestones.Count; index++)
        {
            var milestone = milestones[index];
            var allocation = index == milestones.Count - 1
                ? remainingFee
                : decimal.Round(
                    totalFee * milestone.Amount / totalAmount,
                    4,
                    MidpointRounding.AwayFromZero);
            allocation = Math.Min(allocation, remainingFee);
            allocations[milestone.MilestonesId] = allocation;
            remainingFee -= allocation;
        }

        return allocations;
    }

    private static void QueueTemplateRegeneration(ProjectReceipt receipt, DateTime now)
    {
        receipt.TemplateVersion = CurrentTemplateVersion;
        receipt.GenerationStatus = (int)ProjectReceiptGenerationStatus.Pending;
        receipt.GenerationAttemptCount = 0;
        receipt.NextGenerationAttemptAt = now;
        receipt.GenerationLeaseToken = null;
        receipt.GenerationLeaseExpiresAt = null;
        receipt.GenerationLastError = null;
        receipt.PdfContent = null;
        receipt.PdfFileName = null;
        receipt.PdfContentType = null;
        receipt.PdfSizeBytes = null;
        receipt.PdfHashSha256 = null;
        receipt.GeneratedAt = null;
        receipt.NotificationAttemptCount = 0;
        receipt.NextNotificationAttemptAt = now;
        receipt.NotificationLastError = null;
        receipt.DeliveryLeaseExpiresAt = null;
        receipt.EmailStatus = (int)ProjectReceiptEmailStatus.Pending;
        receipt.EmailAttemptCount = 0;
        receipt.NextEmailAttemptAt = now;
        receipt.EmailLastError = null;
        receipt.EmailedAt = null;
        receipt.UpdatedAt = now;
    }

    private static async Task<string?> ResolveIdentityCodeAsync(
        IApplicationDbContext context,
        Guid contractId,
        User user,
        CancellationToken cancellationToken)
    {
        var signatureIdentityCode = await context.Set<EsignSignature>()
            .AsNoTracking()
            .Where(signature =>
                signature.UserId == user.UserId &&
                signature.EsignDocuments.ContractsId == contractId &&
                signature.IdentityOrTaxCode != null)
            .OrderByDescending(signature =>
                signature.SignedAt ?? signature.DraftSubmittedAt ?? signature.UpdatedAt ?? signature.CreatedAt)
            .Select(signature => signature.IdentityOrTaxCode)
            .FirstOrDefaultAsync(cancellationToken);

        if (ContractIdentityCode.IsValid(signatureIdentityCode))
        {
            return ContractIdentityCode.Normalize(signatureIdentityCode);
        }

        return ContractIdentityCode.IsValid(user.IdentityOrTaxCode)
            ? ContractIdentityCode.Normalize(user.IdentityOrTaxCode)
            : null;
    }

    private static string BuildReceiptNumber(Guid receiptId, ProjectReceiptType type, DateTime issuedAt)
    {
        var role = type == ProjectReceiptType.Client ? "CL" : "FL";
        return $"GB-RC-{issuedAt:yyyyMMdd}-{receiptId:N}-{role}".ToUpperInvariant();
    }

    private static string ComputeSha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
