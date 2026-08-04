namespace Application.Common.Options;

public sealed class WalletWithdrawalOptions
{
    public const string SectionName = "WalletWithdrawals";

    public bool Enabled { get; set; }

    public decimal MinTokens { get; set; } = 10m;

    public decimal MaxTokens { get; set; } = 100_000m;

    public decimal DailyMaxTokens { get; set; } = 500_000m;

    public decimal FixedFeeVnd { get; set; }

    public string Provider { get; set; } = "PayOS";

    public int OutboxBatchSize { get; set; } = 20;

    public int ProcessingTimeoutMinutes { get; set; } = 10;

    public int SyncIntervalMinutes { get; set; } = 5;
}
