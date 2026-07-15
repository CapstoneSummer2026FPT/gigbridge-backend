using Application.Common.Models;
using Application.Features.Wallets.Common.BankAccounts.Create;
using Application.Features.Wallets.Common.BankAccounts.Delete;
using Application.Features.Wallets.Common.BankAccounts.Get;
using Application.Features.Wallets.Common.BankAccounts.Update;
using Application.Features.Wallets.Common.DTOs;
using Application.Features.Wallets.Common.FinancialOverview.DTOs;
using Application.Features.Wallets.Common.FinancialOverview.Queries;
using Application.Features.Wallets.Common.GetMine.Queries;
using Application.Features.Wallets.Common.GetTransactions.Queries;
using Application.Features.Wallets.Common.TopUps.Confirm.Commands;
using Application.Features.Wallets.Common.TopUps.Create.Commands;
using Application.Features.Wallets.Common.TopUps.Sync.Commands;
using Application.Features.Wallets.Common.Withdrawals.Create;
using Application.Features.Wallets.Common.Withdrawals.Get;
using Application.Features.Wallets.Common.Withdrawals.GetDetail;
using Application.Features.Wallets.Common.Withdrawals.Sync;
using Application.Common.Interfaces.IService;
using Application.Common.Options;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Domain.Services.Payments;
using Microsoft.Extensions.Options;

namespace Project_API.Controllers.Wallets.Common;

[ApiController]
[Route("api/wallet")]
[Authorize]
public sealed class WalletController : BaseApiController
{
    private readonly WalletWithdrawalOptions _withdrawalOptions;
    private readonly ISupportedBankDirectory _bankDirectory;

    public WalletController(
        IOptions<WalletWithdrawalOptions> withdrawalOptions,
        ISupportedBankDirectory bankDirectory)
    {
        _withdrawalOptions = withdrawalOptions.Value;
        _bankDirectory = bankDirectory;
    }

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

    [HttpGet("withdrawal-settings")]
    [Authorize(Roles = nameof(UserRole.Freelancer))]
    public IActionResult GetWithdrawalSettings()
    {
        var result = new WithdrawalSettingsResponse(
            _withdrawalOptions.Enabled,
            TokenWalletRules.VndPerToken,
            _withdrawalOptions.FixedFeeVnd,
            _withdrawalOptions.MinTokens,
            _withdrawalOptions.MaxTokens,
            _withdrawalOptions.DailyMaxTokens,
            _withdrawalOptions.Provider);
        return Ok(ApiResponse<WithdrawalSettingsResponse>.Ok(result, "Success"));
    }

    [HttpGet("supported-banks")]
    [Authorize(Roles = nameof(UserRole.Freelancer))]
    public async Task<IActionResult> GetSupportedBanks()
    {
        var banks = await _bankDirectory.GetBanksAsync(HttpContext.RequestAborted);
        var result = banks.Select(bank => new SupportedBankResponse(
            bank.Bin, bank.Code, bank.ShortName, bank.Name, bank.Logo)).ToArray();
        return Ok(ApiResponse<IReadOnlyList<SupportedBankResponse>>.Ok(result, "Success"));
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

}
