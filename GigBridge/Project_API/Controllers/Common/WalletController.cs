using Application.Common.Models;
using Application.Features.Wallets.Common.DTOs;
using Application.Features.Wallets.Common.FinancialOverview.DTOs;
using Application.Features.Wallets.Common.FinancialOverview.Queries;
using Application.Features.Wallets.Common.GetMine.Queries;
using Application.Features.Wallets.Common.GetTransactions.Queries;
using Application.Features.Wallets.Common.TopUps.Confirm.Commands;
using Application.Features.Wallets.Common.TopUps.Create.Commands;
using Application.Features.Wallets.Common.TopUps.Sync.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Common;

[ApiController]
[Route("api/wallet")]
[Authorize]
public sealed class WalletController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetMyWallet()
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GetMyWalletQuery(userId));

        return Ok(ApiResponse<WalletResponse>.Ok(result, "Success"));
    }

    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactions([FromQuery] int limit = 50)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GetWalletTransactionsQuery(userId, limit));

        return Ok(ApiResponse<IReadOnlyList<WalletTransactionResponse>>.Ok(result, "Success"));
    }

    [HttpGet("financial-overview")]
    [Authorize(Roles = "Client,Freelancer")]
    public async Task<IActionResult> GetFinancialOverview(
        [FromQuery] FinancialOverviewPeriod period = FinancialOverviewPeriod.Month)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GetFinancialOverviewQuery(userId, period));

        return Ok(ApiResponse<FinancialOverviewResponse>.Ok(result, "Success"));
    }

    [HttpPost("top-ups")]
    public async Task<IActionResult> CreateTopUp([FromBody] CreateWalletTopUpRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new CreateWalletTopUpCommand(userId, request));

        return Ok(ApiResponse<CreateWalletTopUpResponse>.Ok(result, "Wallet top-up request created"));
    }

    [HttpPost("top-ups/payos/sync")]
    public async Task<IActionResult> SyncPayOsTopUp([FromBody] SyncPayOsTopUpRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new SyncWalletTopUpCommand(userId, request));

        return Ok(ApiResponse<WalletTransactionResponse>.Ok(result, "Wallet top-up status synced"));
    }

    [HttpPost("top-ups/payos/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmPayOsTopUp([FromBody] PayOsTopUpCallbackRequest request)
    {
        var result = await Mediator.Send(new ConfirmWalletTopUpCommand(request));

        return Ok(ApiResponse<WalletTransactionResponse>.Ok(result, "Wallet top-up callback processed"));
    }
}
