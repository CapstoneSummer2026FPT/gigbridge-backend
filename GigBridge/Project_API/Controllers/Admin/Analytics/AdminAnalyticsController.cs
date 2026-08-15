using System.Text;
using Application.Common.Models;
using Application.Common.InternalServices.Admin.Analytics.Models;
using Application.Features.Admin.Analytics.ExportTransactions.Queries;
using Application.Features.Admin.Analytics.GetFinance.Queries;
using Application.Features.Admin.Analytics.GetMarketplace.Queries;
using Application.Features.Admin.Analytics.GetPremium.Queries;
using Application.Features.Admin.Analytics.GetTransactions.Queries;
using Domain.Enums.Accounts;
using Domain.Enums.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Admin.Analytics;

[ApiController]
[Route("api/admin/analytics")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminAnalyticsController : BaseApiController
{
    [HttpGet("finance")]
    public async Task<IActionResult> Finance(
        [FromQuery] string period = "month",
        [FromQuery] DateOnly? anchor = null,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(
            new GetFinanceAnalyticsQuery(new(period, anchor, from, to)), cancellationToken);
        return Ok(ApiResponse<FinanceAnalyticsResponse>.Ok(result, "Platform finance analytics loaded."));
    }

    [HttpGet("premium")]
    public async Task<IActionResult> Premium(
        [FromQuery] string period = "month",
        [FromQuery] DateOnly? anchor = null,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(
            new GetPremiumAnalyticsQuery(new(period, anchor, from, to)), cancellationToken);
        return Ok(ApiResponse<PremiumAnalyticsResponse>.Ok(result, "Premium analytics loaded."));
    }

    [HttpGet("transactions")]
    public async Task<IActionResult> Transactions(
        [FromQuery] string period = "month",
        [FromQuery] DateOnly? anchor = null,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] Guid? userId = null,
        [FromQuery] Guid? contractId = null,
        [FromQuery] int? type = null,
        [FromQuery] int? status = null,
        [FromQuery] string? gateway = null,
        [FromQuery] PlatformRevenueSource? revenueSource = null,
        [FromQuery] string? cursor = null,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(new GetAdminTransactionsQuery(new(
            new(period, anchor, from, to), userId, contractId, type, status, gateway, revenueSource, cursor, pageSize)),
            cancellationToken);
        return Ok(ApiResponse<AdminTransactionPage>.Ok(result, "Wallet ledger analytics loaded."));
    }

    [HttpGet("transactions/export")]
    public async Task<IActionResult> ExportTransactions(
        [FromQuery] string period = "month",
        [FromQuery] DateOnly? anchor = null,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] Guid? userId = null,
        [FromQuery] Guid? contractId = null,
        [FromQuery] int? type = null,
        [FromQuery] int? status = null,
        [FromQuery] string? gateway = null,
        [FromQuery] PlatformRevenueSource? revenueSource = null,
        CancellationToken cancellationToken = default)
    {
        var csv = await Mediator.Send(new ExportAdminTransactionsQuery(new(
            new(period, anchor, from, to), userId, contractId, type, status, gateway, revenueSource, null, 100)),
            cancellationToken);
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"platform-transactions-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
    }

    [HttpGet("marketplace")]
    public async Task<IActionResult> Marketplace(
        [FromQuery] string period = "month",
        [FromQuery] DateOnly? anchor = null,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(
            new GetMarketplaceAnalyticsQuery(new(period, anchor, from, to)), cancellationToken);
        return Ok(ApiResponse<MarketplaceAnalyticsResponse>.Ok(result, "Marketplace analytics loaded."));
    }
}
