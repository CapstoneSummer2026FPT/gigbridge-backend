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
        var (periodStartUtc, periodEndUtc, localPeriodStart) = GetPeriodBounds(
            request.Period,
            _dateTimeService.UtcNow,
            timeZone);
        var serviceFeePrefix = isClient
            ? ServiceFeeWorkflow.EndProjectFeePrefix
            : ServiceFeeWorkflow.AcceptJobFeePrefix;

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
                  transaction.IdempotencyKey.StartsWith(serviceFeePrefix))))
            .Select(transaction => new FinancialTransactionRecord(
                transaction.WalletTransactionsId,
                transaction.ContractsId!.Value,
                transaction.Contract != null ? transaction.Contract.Title : "Project",
                transaction.Type,
                transaction.TokenAmount,
                transaction.IdempotencyKey,
                transaction.CompletedAt ?? transaction.CreatedAt))
            .ToListAsync(cancellationToken);

        var releaseTransactions = transactions
            .Where(transaction => transaction.Type == (int)WalletTransactionType.EscrowRelease)
            .ToList();
        var serviceFeeTransactions = transactions
            .Where(transaction => IsServiceFee(transaction, serviceFeePrefix))
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
            : await _context.Set<WalletTransaction>()
                .AsNoTracking()
                .Where(transaction =>
                    transaction.UserId == request.UserId &&
                    transaction.Status == (int)WalletTransactionStatus.Succeeded &&
                    transaction.Type == (int)WalletTransactionType.EscrowRelease &&
                    transaction.ContractsId.HasValue &&
                    progressContractIds.Contains(transaction.ContractsId.Value))
                .SumAsync(transaction => transaction.TokenAmount, cancellationToken);
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
            timeZone,
            serviceFeePrefix);
        var recentTransactions = transactions
            .OrderByDescending(transaction => transaction.OccurredAt)
            .Take(RecentTransactionLimit)
            .Select(transaction => ToTransactionItem(transaction, isClient, serviceFeePrefix))
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
        TimeZoneInfo timeZone,
        string serviceFeePrefix)
    {
        var bucketStarts = period switch
        {
            FinancialOverviewPeriod.Day => Enumerable.Range(0, 24)
                .Select(hour => localPeriodStart.AddHours(hour)),
            FinancialOverviewPeriod.Month => Enumerable.Range(
                    0,
                    DateTime.DaysInMonth(localPeriodStart.Year, localPeriodStart.Month))
                .Select(day => localPeriodStart.AddDays(day)),
            FinancialOverviewPeriod.Year => Enumerable.Range(0, 12)
                .Select(month => localPeriodStart.AddMonths(month)),
            _ => throw new BadRequestException("Unsupported financial overview period.")
        };

        return bucketStarts.Select(bucketStart =>
        {
            var bucketEnd = period switch
            {
                FinancialOverviewPeriod.Day => bucketStart.AddHours(1),
                FinancialOverviewPeriod.Month => bucketStart.AddDays(1),
                FinancialOverviewPeriod.Year => bucketStart.AddMonths(1),
                _ => bucketStart
            };
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
                FinancialOverviewPeriod.Year => bucketStart.ToString("MMM"),
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
                    .Where(transaction => IsServiceFee(transaction, serviceFeePrefix))
                    .Sum(transaction => transaction.Amount));
        }).ToList();
    }

    private static FinancialTransactionItem ToTransactionItem(
        FinancialTransactionRecord transaction,
        bool isClient,
        string serviceFeePrefix)
    {
        var category = transaction.Type switch
        {
            (int)WalletTransactionType.EscrowHold => "escrow",
            (int)WalletTransactionType.EscrowRelease => "released",
            (int)WalletTransactionType.EscrowRefund => "refund",
            _ when IsServiceFee(transaction, serviceFeePrefix) => "serviceFee",
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
        string serviceFeePrefix)
    {
        return transaction.Type == (int)WalletTransactionType.Adjustment &&
            transaction.IdempotencyKey?.StartsWith(serviceFeePrefix, StringComparison.Ordinal) == true;
    }

    private static (DateTime StartUtc, DateTime EndUtc, DateTime LocalStart) GetPeriodBounds(
        FinancialOverviewPeriod period,
        DateTime utcNow,
        TimeZoneInfo timeZone)
    {
        var normalizedUtcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(normalizedUtcNow, timeZone);
        var localStart = period switch
        {
            FinancialOverviewPeriod.Day => localNow.Date,
            FinancialOverviewPeriod.Month => new DateTime(localNow.Year, localNow.Month, 1),
            FinancialOverviewPeriod.Year => new DateTime(localNow.Year, 1, 1),
            _ => throw new BadRequestException("Unsupported financial overview period.")
        };
        var localEnd = period switch
        {
            FinancialOverviewPeriod.Day => localStart.AddDays(1),
            FinancialOverviewPeriod.Month => localStart.AddMonths(1),
            FinancialOverviewPeriod.Year => localStart.AddYears(1),
            _ => localStart
        };

        return (
            TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(localStart, DateTimeKind.Unspecified),
                timeZone),
            TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(localEnd, DateTimeKind.Unspecified),
                timeZone),
            localStart);
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
