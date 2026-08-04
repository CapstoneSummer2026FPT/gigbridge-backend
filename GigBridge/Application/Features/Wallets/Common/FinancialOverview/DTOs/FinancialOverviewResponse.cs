namespace Application.Features.Wallets.Common.FinancialOverview.DTOs;

public enum FinancialOverviewPeriod
{
    Day = 0,
    Month = 1,
    Year = 2
}

public sealed record FinancialTrendPoint(
    string Period,
    DateTime PeriodStartUtc,
    decimal PaidOrReceivedAmount,
    decimal EscrowFundedAmount,
    decimal ServiceFeeAmount);

public sealed record FinancialTransactionItem(
    Guid WalletTransactionId,
    Guid ContractId,
    string Project,
    string Category,
    decimal Amount,
    decimal SignedAmount,
    DateTime OccurredAt);

public sealed record FinancialOverviewResponse(
    string Role,
    string Period,
    DateTime PeriodStartUtc,
    DateTime PeriodEndUtc,
    decimal TotalAmount,
    decimal AverageAmount,
    decimal ProgressAmount,
    decimal TotalContractValue,
    decimal ProgressPercentage,
    decimal TotalServiceFeePaid,
    int AverageDivisorJobCount,
    IReadOnlyList<FinancialTrendPoint> TrendPoints,
    IReadOnlyList<FinancialTransactionItem> RecentTransactions);
