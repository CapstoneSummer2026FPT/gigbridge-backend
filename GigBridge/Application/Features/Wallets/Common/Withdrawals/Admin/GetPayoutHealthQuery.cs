using Application.Features.Wallets.Common.DTOs;
using MediatR;

namespace Application.Features.Wallets.Common.Withdrawals.Admin;

/// <param name="BypassCache">
/// Skips the short availability cache. Use it right after changing PayOS credentials or the IP
/// allowlist, otherwise a stale failure can be reported for up to another 30 seconds.
/// </param>
public sealed record GetPayoutHealthQuery(bool BypassCache) : IRequest<PayoutHealthResponse>;
