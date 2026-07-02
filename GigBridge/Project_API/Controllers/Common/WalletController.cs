using Application.Common.Models;
using Application.Features.Wallets.Common.BankAccounts.Create;
using Application.Features.Wallets.Common.BankAccounts.Delete;
using Application.Features.Wallets.Common.BankAccounts.Get;
using Application.Features.Wallets.Common.BankAccounts.Update;
using Application.Features.Wallets.Common.DTOs;
using Application.Features.Wallets.Common.GetMine.Queries;
using Application.Features.Wallets.Common.GetTransactions.Queries;
using Application.Features.Wallets.Common.TopUps.Confirm.Commands;
using Application.Features.Wallets.Common.TopUps.Create.Commands;
using Application.Features.Wallets.Common.TopUps.Sync.Commands;
using Application.Features.Wallets.Common.Withdrawals.Create;
using Application.Features.Wallets.Common.Withdrawals.Get;
using Application.Features.Wallets.Common.Withdrawals.GetDetail;
using Application.Features.Wallets.Common.Withdrawals.Sync;
using Application.Features.Wallets.Common.Withdrawals.Webhook;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

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

    [HttpGet("bank-accounts")]
    [Authorize(Roles = nameof(UserRole.Freelancer))]
    public async Task<IActionResult> GetBankAccounts()
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GetBankAccountsQuery(userId));
        return Ok(ApiResponse<IReadOnlyList<BankAccountResponse>>.Ok(result, "Success"));
    }

    [HttpPost("bank-accounts")]
    [Authorize(Roles = nameof(UserRole.Freelancer))]
    public async Task<IActionResult> CreateBankAccount([FromBody] CreateBankAccountRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new CreateBankAccountCommand(userId, request));
        return Ok(ApiResponse<BankAccountResponse>.Ok(result, "Bank account created"));
    }

    [HttpPatch("bank-accounts/{bankAccountId:guid}")]
    [Authorize(Roles = nameof(UserRole.Freelancer))]
    public async Task<IActionResult> UpdateBankAccount(
        Guid bankAccountId,
        [FromBody] UpdateBankAccountRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new UpdateBankAccountCommand(userId, bankAccountId, request));
        return Ok(ApiResponse<BankAccountResponse>.Ok(result, "Bank account updated"));
    }

    [HttpDelete("bank-accounts/{bankAccountId:guid}")]
    [Authorize(Roles = nameof(UserRole.Freelancer))]
    public async Task<IActionResult> DeleteBankAccount(Guid bankAccountId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        await Mediator.Send(new DeleteBankAccountCommand(userId, bankAccountId));
        return Ok(ApiResponse<object>.Ok(new { }, "Bank account deleted"));
    }

    [HttpPost("withdrawals")]
    [Authorize(Roles = nameof(UserRole.Freelancer))]
    public async Task<IActionResult> CreateWithdrawal([FromBody] CreateWithdrawalRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new CreateWithdrawalCommand(userId, request));
        return Ok(ApiResponse<WithdrawalResponse>.Ok(result, "Withdrawal request created"));
    }

    [HttpGet("withdrawals")]
    [Authorize(Roles = nameof(UserRole.Freelancer))]
    public async Task<IActionResult> GetWithdrawals([FromQuery] int limit = 50)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GetWithdrawalsQuery(userId, limit));
        return Ok(ApiResponse<IReadOnlyList<WithdrawalResponse>>.Ok(result, "Success"));
    }

    [HttpGet("withdrawals/{withdrawalId:guid}")]
    [Authorize(Roles = nameof(UserRole.Freelancer))]
    public async Task<IActionResult> GetWithdrawalDetail(Guid withdrawalId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GetWithdrawalDetailQuery(userId, withdrawalId));
        return Ok(ApiResponse<WithdrawalResponse>.Ok(result, "Success"));
    }

    [HttpPost("withdrawals/{withdrawalId:guid}/sync")]
    [Authorize(Roles = nameof(UserRole.Freelancer))]
    public async Task<IActionResult> SyncWithdrawal(Guid withdrawalId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new SyncWithdrawalCommand(withdrawalId, userId, false));
        return Ok(ApiResponse<WithdrawalResponse>.Ok(result, "Withdrawal status synced"));
    }

    [HttpPost("withdrawals/payos/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmPayOsWithdrawal([FromBody] JsonElement request)
    {
        var signature =
            Request.Headers["x-payos-signature"].FirstOrDefault() ??
            Request.Headers["payos-signature"].FirstOrDefault() ??
            Request.Headers["signature"].FirstOrDefault();

        var result = await Mediator.Send(new HandlePayoutWebhookCommand(request.GetRawText(), signature));
        return Ok(ApiResponse<PayoutWebhookResponse>.Ok(result, "Withdrawal callback processed"));
    }
}
