using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Wallets.Interfaces;
using Application.Common.Options;
using Application.Features.Wallets.Common.DTOs;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Application.Features.Wallets.Common.Withdrawals.Admin;

public sealed class GetPayoutHealthQueryHandler
    : IRequestHandler<GetPayoutHealthQuery, PayoutHealthResponse>
{
    private readonly IPayoutProvider _payoutProvider;
    private readonly IPayoutDiagnostics _payoutDiagnostics;
    private readonly WalletWithdrawalOptions _options;
    private readonly IConfiguration _configuration;
    private readonly IDateTimeService _dateTimeService;

    public GetPayoutHealthQueryHandler(
        IPayoutProvider payoutProvider,
        IPayoutDiagnostics payoutDiagnostics,
        IOptions<WalletWithdrawalOptions> options,
        IConfiguration configuration,
        IDateTimeService dateTimeService)
    {
        _payoutProvider = payoutProvider;
        _payoutDiagnostics = payoutDiagnostics;
        _options = options.Value;
        _configuration = configuration;
        _dateTimeService = dateTimeService;
    }

    public async Task<PayoutHealthResponse> Handle(
        GetPayoutHealthQuery query,
        CancellationToken cancellationToken)
    {
        var availability = await _payoutProvider.CheckAvailabilityAsync(
            cancellationToken,
            query.BypassCache);
        var diagnostics = await _payoutDiagnostics.DescribeAsync(cancellationToken);

        return new PayoutHealthResponse(
            _payoutProvider.ProviderName,
            Environment.MachineName,
            _options.Enabled,
            BackgroundWorkerOptions.IsEnabled(_configuration),
            availability.IsAvailable,
            availability.BalanceVnd,
            availability.ErrorCode,
            availability.SafeMessage,
            diagnostics.CredentialsConfigured,
            diagnostics.ClientIdPrefix,
            diagnostics.ProxyConfigured,
            diagnostics.OutboundIp,
            diagnostics.OutboundIpError,
            _dateTimeService.UtcNow);
    }
}
