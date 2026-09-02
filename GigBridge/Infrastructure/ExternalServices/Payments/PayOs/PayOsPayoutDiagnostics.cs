using Application.Common.InternalServices.Wallets.Interfaces;
using Application.Common.InternalServices.Wallets.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.ExternalServices.Payments.PayOs;

public sealed class PayOsPayoutDiagnostics : IPayoutDiagnostics
{
    private const string OutboundIpProbeUrl = "https://checkip.amazonaws.com";
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);

    private readonly PayOsPayoutOptions _options;
    private readonly ILogger<PayOsPayoutDiagnostics> _logger;

    public PayOsPayoutDiagnostics(
        IOptions<PayOsPayoutOptions> options,
        ILogger<PayOsPayoutDiagnostics> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PayoutProviderDiagnostics> DescribeAsync(CancellationToken cancellationToken)
    {
        var credentialsConfigured =
            !string.IsNullOrWhiteSpace(_options.ClientId) &&
            !string.IsNullOrWhiteSpace(_options.ApiKey) &&
            !string.IsNullOrWhiteSpace(_options.ChecksumKey);
        var proxyConfigured = !string.IsNullOrWhiteSpace(_options.ProxyUrl);
        var (outboundIp, probeError) = await ProbeOutboundIpAsync(cancellationToken);

        return new PayoutProviderDiagnostics(
            credentialsConfigured,
            Prefix(_options.ClientId),
            proxyConfigured,
            outboundIp,
            probeError);
    }

    /// <summary>
    /// Resolves the public address the payout provider sees, using the same handler the payout
    /// client is built with so a proxy or NAT is reflected. Best effort: a probe failure is
    /// reported alongside the rest of the diagnostics rather than failing the whole request.
    /// </summary>
    private async Task<(string? OutboundIp, string? Error)> ProbeOutboundIpAsync(
        CancellationToken cancellationToken)
    {
        using var handler = string.IsNullOrWhiteSpace(_options.ProxyUrl)
            ? (HttpMessageHandler)DependencyInjection.CreatePayoutDirectHandler()
            : DependencyInjection.CreatePayoutProxyHandler(_options.ProxyUrl);
        using var client = new HttpClient(handler, disposeHandler: false) { Timeout = ProbeTimeout };

        try
        {
            var response = await client.GetStringAsync(OutboundIpProbeUrl, cancellationToken);
            return (response.Trim(), null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "Outbound IP probe failed. ExceptionType={ExceptionType}",
                ex.GetType().Name);
            return (null, $"Outbound IP probe failed ({ex.GetType().Name}).");
        }
    }

    private static string? Prefix(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Length <= 8 ? value : value[..8];
}
