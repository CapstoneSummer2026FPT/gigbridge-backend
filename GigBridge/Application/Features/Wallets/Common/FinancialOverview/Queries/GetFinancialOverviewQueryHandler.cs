using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Wallets.Common.FinancialOverview.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Wallets.Common.FinancialOverview.Queries;

public sealed class GetFinancialOverviewQueryHandler :
    IRequestHandler<GetFinancialOverviewQuery, FinancialOverviewResponse>
{
    private const int RecentTransactionLimit = 20;

    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public GetFinancialOverviewQueryHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<FinancialOverviewResponse> Handle(
        GetFinancialOverviewQuery request,
        CancellationToken cancellationToken)
    {
        var role = await _context.Set<User>()
            .AsNoTracking()
            .Where(user => user.UserId == request.UserId)
            .Select(user => (int?)user.Role)
            .FirstOrDefaultAsync(cancellationToken);

        if (!role.HasValue)
        {
            throw new NotFoundException("User does not exist.");
        }

        if (role.Value is not (int)UserRole.Client and not (int)UserRole.Freelancer)
        {
            throw new ForbiddenAccessException("Financial overview is only available to clients and freelancers.");
        }

        var isClient = role.Value == (int)UserRole.Client;
        var timeZone = GetVietnamTimeZone();
        var (periodStartUtc, periodEndUtc, localPeriodStart, localPeriodEnd) = GetPeriodBounds(
            request.Period,
            _dateTimeService.UtcNow,
            timeZone);
        IReadOnlyList<string> serviceFeePrefixes = isClient
            ? [ServiceFeeWorkflow.ClientFundingFeePrefix, ServiceFeeWorkflow.EndProjectFeePrefix]
            : [ServiceFeeWorkflow.FreelancerReleaseFeePrefix, ServiceFeeWorkflow.AcceptJobFeePrefix];

        var transactions = await _context.Set<WalletTransaction>()
            .AsNoTracking()
            .Where(transaction =>
                transaction.UserId == request.UserId &&
                transaction.Status == (int)WalletTransactionStatus.Succeeded &&
                transaction.ContractsId.HasValue &&
                (transaction.CompletedAt ?? transaction.CreatedAt) >= periodStartUtc &&
                (transaction.CompletedAt ?? transaction.CreatedAt) < periodEndUtc &&
                (transaction.Type == (int)WalletTransactionType.EscrowRelease ||
                 (isClient && transaction.Type == (int)WalletTransactionType.EscrowHold) ||
                 (isClient && transaction.Type == (int)WalletTransactionType.EscrowRefund) ||
                 (transaction.Type == (int)WalletTransactionType.Adjustment &&
                  transaction.IdempotencyKey != null &&
                  (transaction.IdempotencyKey.StartsWith(serviceFeePrefixes[0]) ||
                   transaction.IdempotencyKey.StartsWith(serviceFeePrefixes[1])))))
            .Select(transaction => new FinancialTransactionRecord(
                transaction.WalletTransactionsId,
                transaction.ContractsId!.Value,
                transaction.Contract != null ? transaction.Contract.Title : "Project",
                transaction.Type,
                transaction.VndAmount,
                transaction.IdempotencyKey,
                transaction.CompletedAt ?? transaction.CreatedAt))
            .ToListAsync(cancellationToken);

        var releaseTransactions = transactions
            .Where(transaction => transaction.Type == (int)WalletTransactionType.EscrowRelease)
            .ToList();
        var serviceFeeTransactions = transactions
            .Where(transaction => IsServiceFee(transaction, serviceFeePrefixes))
            .ToList();

        var totalAmount = releaseTransactions.Sum(transaction => transaction.Amount);
        var averageDivisorJobCount = isClient
            ? releaseTransactions.Select(transaction => transaction.ContractId).Distinct().Count()
            : await CountCompletedFreelancerContracts(
                request.UserId,
                periodStartUtc,
                periodEndUtc,
                cancellationToken);
        var averageAmount = averageDivisorJobCount == 0
            ? 0m
            : decimal.Round(totalAmount / averageDivisorJobCount, 4, MidpointRounding.AwayFromZero);
        var totalServiceFeePaid = serviceFeeTransactions.Sum(transaction => transaction.Amount);

        var relevantContractIds = transactions
            .Select(transaction => transaction.ContractId)
            .Distinct()
            .ToArray();
        var relevantContracts = relevantContractIds.Length == 0
            ? []
            : await _context.Set<Contract>()
                .AsNoTracking()
                .Where(contract =>
                    relevantContractIds.Contains(contract.ContractsId) &&
                    contract.Status != (int)ContractStatus.Cancelled &&
                    contract.Status != (int)ContractStatus.Disputed)
                .Select(contract => new { contract.ContractsId, contract.TotalBudget })
                .ToListAsync(cancellationToken);
        var progressContractIds = relevantContracts
            .Select(contract => contract.ContractsId)
            .ToArray();
        var totalContractValue = relevantContracts.Sum(contract => contract.TotalBudget);
        var progressAmount = progressContractIds.Length == 0
            ? 0m
            : await _context.Set<ContractEscrow>()
                .AsNoTracking()
                .Where(escrow => progressContractIds.Contains(escrow.ContractsId))
                .SumAsync(escrow => escrow.ReleasedAmount, cancellationToken);
        var progressPercentage = totalContractValue <= 0m
            ? 0m
            : decimal.Round(
                Math.Clamp(progressAmount / totalContractValue * 100m, 0m, 100m),
                2,
                MidpointRounding.AwayFromZero);

        var trendPoints = BuildTrendPoints(
            transactions,
            request.Period,
            localPeriodStart,
            localPeriodEnd,
            timeZone,
            serviceFeePrefixes);
        var recentTransactions = transactions
            .OrderByDescending(transaction => transaction.OccurredAt)
            .Take(RecentTransactionLimit)
            .Select(transaction => ToTransactionItem(transaction, isClient, serviceFeePrefixes))
            .ToList();

        return new FinancialOverviewResponse(
            isClient ? "Client" : "Freelancer",
            request.Period.ToString().ToLowerInvariant(),
            periodStartUtc,
            periodEndUtc,
            totalAmount,
            averageAmount,
            progressAmount,
            totalContractValue,
            progressPercentage,
            totalServiceFeePaid,
            averageDivisorJobCount,
            trendPoints,
            recentTransactions);
    }

    private async Task<int> CountCompletedFreelancerContracts(
        Guid userId,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        CancellationToken cancellationToken)
    {
        return await _context.Set<Contract>()
            .AsNoTracking()
            .CountAsync(contract =>
                contract.Status == (int)ContractStatus.Completed &&
                contract.CompletedAt.HasValue &&
                contract.CompletedAt.Value >= periodStartUtc &&
                contract.CompletedAt.Value < periodEndUtc &&
                contract.FreelancerProfiles != null &&
                contract.FreelancerProfiles.UserId == userId,
                cancellationToken);
    }

    private static IReadOnlyList<FinancialTrendPoint> BuildTrendPoints(
        IReadOnlyCollection<FinancialTransactionRecord> transactions,
        FinancialOverviewPeriod period,
        DateTime localPeriodStart,
        DateTime localPeriodEnd,
        TimeZoneInfo timeZone,
        IReadOnlyCollection<string> serviceFeePrefixes)
    {
        IReadOnlyList<DateTime> bucketStarts = period switch
        {
            FinancialOverviewPeriod.Day => Enumerable.Range(0, 24)
                .Select(hour => localPeriodStart.AddHours(hour))
                .ToList(),
            FinancialOverviewPeriod.Month => Enumerable.Range(0, Math.Max(
                    1,
                    (int)Math.Ceiling((localPeriodEnd - localPeriodStart).TotalDays)))
                .Select(day => localPeriodStart.AddDays(day))
                .Where(bucketStart => bucketStart < localPeriodEnd)
                .ToList(),
            FinancialOverviewPeriod.Year => Enumerable.Range(0, 12)
                .Select(month => localPeriodEnd.AddMonths(month - 12))
                .ToList(),
            _ => throw new BadRequestException("Unsupported financial overview period.")
        };

        return bucketStarts.Select((bucketStart, index) =>
        {
            var bucketEnd = index + 1 < bucketStarts.Count
                ? bucketStarts[index + 1]
                : localPeriodEnd;
            var bucketTransactions = transactions.Where(transaction =>
            {
                var localOccurredAt = TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.SpecifyKind(transaction.OccurredAt, DateTimeKind.Utc),
                    timeZone);
                return localOccurredAt >= bucketStart && localOccurredAt < bucketEnd;
            }).ToList();
            var label = period switch
            {
                FinancialOverviewPeriod.Day => bucketStart.ToString("HH:mm"),
                FinancialOverviewPeriod.Month => bucketStart.ToString("dd MMM"),
                FinancialOverviewPeriod.Year => bucketStart.ToString("MMM yyyy"),
                _ => string.Empty
            };

            return new FinancialTrendPoint(
                label,
                TimeZoneInfo.ConvertTimeToUtc(
                    DateTime.SpecifyKind(bucketStart, DateTimeKind.Unspecified),
                    timeZone),
                bucketTransactions
                    .Where(transaction => transaction.Type == (int)WalletTransactionType.EscrowRelease)
                    .Sum(transaction => transaction.Amount),
                bucketTransactions
                    .Where(transaction => transaction.Type == (int)WalletTransactionType.EscrowHold)
                    .Sum(transaction => transaction.Amount),
                bucketTransactions
                    .Where(transaction => IsServiceFee(transaction, serviceFeePrefixes))
                    .Sum(transaction => transaction.Amount));
        }).ToList();
    }

    private static FinancialTransactionItem ToTransactionItem(
        FinancialTransactionRecord transaction,
        bool isClient,
        IReadOnlyCollection<string> serviceFeePrefixes)
    {
        var category = transaction.Type switch
        {
            (int)WalletTransactionType.EscrowHold => "escrow",
            (int)WalletTransactionType.EscrowRelease => "released",
            (int)WalletTransactionType.EscrowRefund => "refund",
            _ when IsServiceFee(transaction, serviceFeePrefixes) => "serviceFee",
            _ => "other"
        };
        var isPositive = transaction.Type == (int)WalletTransactionType.EscrowRefund ||
            (!isClient && transaction.Type == (int)WalletTransactionType.EscrowRelease);

        return new FinancialTransactionItem(
            transaction.TransactionId,
            transaction.ContractId,
            transaction.Project,
            category,
            transaction.Amount,
            isPositive ? transaction.Amount : -transaction.Amount,
            transaction.OccurredAt);
    }

    private static bool IsServiceFee(
        FinancialTransactionRecord transaction,
        IReadOnlyCollection<string> serviceFeePrefixes)
    {
        return transaction.Type == (int)WalletTransactionType.Adjustment &&
            transaction.IdempotencyKey is not null &&
            serviceFeePrefixes.Any(prefix =>
                transaction.IdempotencyKey.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static (
        DateTime StartUtc,
        DateTime EndUtc,
        DateTime LocalStart,
        DateTime LocalEnd) GetPeriodBounds(
        FinancialOverviewPeriod period,
        DateTime utcNow,
        TimeZoneInfo timeZone)
    {
        var normalizedUtcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        var localEnd = TimeZoneInfo.ConvertTimeFromUtc(normalizedUtcNow, timeZone);
        var localStart = period switch
        {
            FinancialOverviewPeriod.Day => localEnd.AddDays(-1),
            FinancialOverviewPeriod.Month => localEnd.AddMonths(-1),
            FinancialOverviewPeriod.Year => localEnd.AddYears(-1),
            _ => throw new BadRequestException("Unsupported financial overview period.")
        };

        return (
            TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(localStart, DateTimeKind.Unspecified),
                timeZone),
            TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(localEnd, DateTimeKind.Unspecified),
                timeZone),
            localStart,
            localEnd);
    }

    private static TimeZoneInfo GetVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
    }

    private sealed record FinancialTransactionRecord(
        Guid TransactionId,
        Guid ContractId,
        string Project,
        int Type,
        decimal Amount,
        string? IdempotencyKey,
        DateTime OccurredAt);
}
