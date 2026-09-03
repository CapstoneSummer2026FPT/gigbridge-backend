using Application.Common.InternalServices.Wallets.Interfaces;
using Application.Common.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.Common.InternalServices.Wallets.BackgroundJobs;

/// <summary>
/// Writes one line at startup describing how withdrawals are configured on this node. The API runs
/// on several nodes behind a load balancer, and a node that is misconfigured - workers off, payout
/// credentials missing - otherwise looks identical to a healthy one in the logs.
/// </summary>
public sealed class PayoutConfigurationReporter : IHostedService
{
    private readonly ILogger<PayoutConfigurationReporter> _logger;
    private readonly WalletWithdrawalOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly bool _backgroundWorkersEnabled;

    public PayoutConfigurationReporter(
        ILogger<PayoutConfigurationReporter> logger,
        IOptions<WalletWithdrawalOptions> options,
        IServiceScopeFactory scopeFactory,
        Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _logger = logger;
        _options = options.Value;
        _scopeFactory = scopeFactory;
        _backgroundWorkersEnabled = BackgroundWorkerOptions.IsEnabled(configuration);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var diagnostics = await scope.ServiceProvider
                .GetRequiredService<IPayoutDiagnostics>()
                .DescribeAsync(cancellationToken);
            var line =
                "Withdrawal configuration on {Instance}: WithdrawalsEnabled={WithdrawalsEnabled} " +
                "BackgroundWorkersEnabled={BackgroundWorkersEnabled} Provider={Provider} " +
                "PayoutCredentialsConfigured={CredentialsConfigured} ClientIdPrefix={ClientIdPrefix} " +
                "ProxyConfigured={ProxyConfigured} OutboundIp={OutboundIp}";

            if (_options.Enabled && !diagnostics.CredentialsConfigured)
            {
                _logger.LogError(
                    line + ". Withdrawals are enabled but payout credentials are missing - every " +
                    "payout will fail.",
                    Environment.MachineName,
                    _options.Enabled,
                    _backgroundWorkersEnabled,
                    _options.Provider,
                    diagnostics.CredentialsConfigured,
                    diagnostics.ClientIdPrefix,
                    diagnostics.ProxyConfigured,
                    diagnostics.OutboundIp);
                return;
            }

            _logger.LogInformation(
                line,
                Environment.MachineName,
                _options.Enabled,
                _backgroundWorkersEnabled,
                _options.Provider,
                diagnostics.CredentialsConfigured,
                diagnostics.ClientIdPrefix,
                diagnostics.ProxyConfigured,
                diagnostics.OutboundIp);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Reporting configuration must never keep the host from starting.
            _logger.LogWarning(ex, "Could not report withdrawal configuration at startup.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
