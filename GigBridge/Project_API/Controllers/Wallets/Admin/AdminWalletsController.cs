using Application.Common.Models;
using Application.Features.Admin.AdminCredit.Commands;
using Application.Features.Admin.AdminCredit.DTOs;
using Application.Features.Wallets.Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Admin;

[ApiController]
[Route("api/admin/wallets")]
[Authorize(Roles = "Admin")]
public sealed class AdminWalletsController : BaseApiController
{
    [HttpGet("{userId}/balance")]
    public async Task<IActionResult> GetWalletBalance(Guid userId)
    {
        if (!TryGetCurrentUserId(out var adminUserId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new Application.Features.Admin.AdminCredit.Queries.GetAdminUserWalletQuery(adminUserId, userId));

        return Ok(ApiResponse<WalletResponse>.Ok(result, "Success"));
    }

    [HttpGet("{userId}/history")]
    public async Task<IActionResult> GetWalletHistory(Guid userId, [FromQuery] int limit = 50)
    {
        if (!TryGetCurrentUserId(out var adminUserId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new Application.Features.Wallets.Common.GetTransactions.Queries.GetWalletTransactionsQuery(userId, limit));

        return Ok(ApiResponse<IReadOnlyList<WalletTransactionResponse>>.Ok(result, "Success"));
    }

    [HttpPost("{userId}/credit")]
    public async Task<IActionResult> CreditWallet(
        Guid userId,
        [FromBody] AdminCreditWalletRequest request)
    {
        if (!TryGetCurrentUserId(out var adminUserId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new AdminCreditWalletCommand(adminUserId, userId, request));

        return Ok(ApiResponse<WalletTransactionResponse>.Ok(result, "Wallet credited"));
    }

    [HttpPost("{userId}/debit")]
    public async Task<IActionResult> DebitWallet(
        Guid userId,
        [FromBody] AdminUpdateWalletRequest request)
    {
        if (!TryGetCurrentUserId(out var adminUserId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new AdminWalletUpdateCommand(adminUserId, userId, request));

        return Ok(ApiResponse<WalletTransactionResponse>.Ok(result, "Wallet updated"));
    }
}

