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
using Application.Common.InternalServices.Wallets.Interfaces;
using Application.Common.Options;
using Domain.Enums.Accounts;
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

    /// <remarks>
    /// <paramref name="limit"/> is clamped server-side to 1..100 — asking for more silently
    /// returns 100. Lifetime totals belong to transactions/summary, not to this window.
    /// </remarks>
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

    /// <remarks>
    /// The response is role-shaped: <c>role</c> ("Client" / "Freelancer" / "Generic") selects
    /// which branch is populated, because only clients fund escrow and only freelancers withdraw.
    /// Left open to every authenticated role — an Admin receives the "Generic" shape covering
    /// their own wallet rather than a 403.
    /// </remarks>
    [HttpGet("transactions/summary")]
    public async Task<IActionResult> GetTransactionsSummary()
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GetWalletTransactionsSummaryQuery(userId));

        return Ok(ApiResponse<WalletTransactionsSummaryResponse>.Ok(result, "Success"));
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
    public async Task<IActionResult> GetWithdrawalSettings()
    {
        var enabled = _withdrawalOptions.Enabled;
        if (enabled)
        {
            var payoutProvider = HttpContext.RequestServices.GetRequiredService<IPayoutProvider>();
            enabled = (await payoutProvider.CheckAvailabilityAsync(HttpContext.RequestAborted)).IsAvailable;
        }

        var result = new WithdrawalSettingsResponse(
            enabled,
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
        return Accepted(ApiResponse<WithdrawalResponse>.Ok(
            result,
            "Withdrawal queued for automatic processing"));
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
