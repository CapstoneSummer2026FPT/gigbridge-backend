namespace Infrastructure.ExternalServices.Payments.PayOs;

public sealed class PayOsOptions
{
    public const string SectionName = "PayOS";

    public string? ClientId { get; set; }

    public string? ApiKey { get; set; }

    public string? ChecksumKey { get; set; }

    public string? ReturnUrl { get; set; }

    public string? CancelUrl { get; set; }

    public string? WebhookUrl { get; set; }

}

public sealed class PayOsPayoutOptions
{
    public const string SectionName = "PayOSPayout";

    public string? ClientId { get; set; }

    public string? ApiKey { get; set; }

    public string? ChecksumKey { get; set; }

    public string? ProxyUrl { get; set; }
}
