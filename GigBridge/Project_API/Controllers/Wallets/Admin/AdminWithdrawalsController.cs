using Application.Common.Models;
using Application.Features.Wallets.Common.DTOs;
using Application.Features.Wallets.Common.Withdrawals.Admin;
using Application.Features.Wallets.Common.Withdrawals.Sync;
using Domain.Enums.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Wallets.Admin;

[ApiController]
[Route("api/admin/withdrawals")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminWithdrawalsController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetWithdrawals(
        [FromQuery] int? status,
        [FromQuery] int limit = 100)
    {
        var result = await Mediator.Send(new GetAdminWithdrawalsQuery(status, limit));
        return Ok(ApiResponse<IReadOnlyList<WithdrawalResponse>>.Ok(result, "Success"));
    }

    /// <summary>
    /// Reports why payouts are or are not flowing on the node that serves this request. Behind a
    /// load balancer, call it a few times and compare <c>instance</c> to cover every node.
    /// </summary>
    [HttpGet("payout-health")]
    public async Task<IActionResult> GetPayoutHealth([FromQuery] bool bypassCache = false)
    {
        var result = await Mediator.Send(new GetPayoutHealthQuery(bypassCache));
        return Ok(ApiResponse<PayoutHealthResponse>.Ok(result, "Success"));
    }

    [HttpGet("{withdrawalId:guid}")]
    public async Task<IActionResult> GetWithdrawalDetail(Guid withdrawalId)
    {
        var result = await Mediator.Send(new GetAdminWithdrawalDetailQuery(withdrawalId));
        return Ok(ApiResponse<WithdrawalResponse>.Ok(result, "Success"));
    }

    [HttpPost("{withdrawalId:guid}/sync")]
    public async Task<IActionResult> SyncWithdrawal(Guid withdrawalId)
    {
        var result = await Mediator.Send(new SyncWithdrawalCommand(withdrawalId, null, true));
        return Ok(ApiResponse<WithdrawalResponse>.Ok(result, "Withdrawal status synced"));
    }

    /// <summary>
    /// Re-reads provider state for every non-terminal withdrawal. Safe to run first after an
    /// outage: it only reads, so it cannot pay twice, and it tells apart withdrawals the provider
    /// already paid from ones it never received.
    /// </summary>
    [HttpPost("bulk-sync")]
    public async Task<IActionResult> BulkSyncWithdrawals(
        [FromQuery] int? status,
        [FromQuery] int limit = 100)
    {
        var result = await Mediator.Send(new BulkSyncWithdrawalsCommand(status, limit));
        return Ok(ApiResponse<BulkWithdrawalOperationResponse>.Ok(result, "Withdrawals synced"));
    }

    /// <summary>
    /// Re-queues an explicit list of withdrawals for payout. Run <c>bulk-sync</c> first and retry
    /// only the ids it reports as never received by the provider.
    /// </summary>
    [HttpPost("bulk-retry")]
    public async Task<IActionResult> BulkRetryWithdrawals(
        [FromBody] BulkRetryWithdrawalsRequest request)
    {
        if (!TryGetCurrentUserId(out var adminUserId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(
            new BulkRetryWithdrawalsCommand(adminUserId, request.WithdrawalIds));
        return Accepted(ApiResponse<BulkWithdrawalOperationResponse>.Ok(
            result,
            "Withdrawal retries queued; worker will process automatically"));
    }

    [HttpPost("{withdrawalId:guid}/retry")]
    public async Task<IActionResult> RetryWithdrawal(Guid withdrawalId)
    {
        if (!TryGetCurrentUserId(out var adminUserId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new RetryWithdrawalCommand(adminUserId, withdrawalId));
        return Accepted(ApiResponse<WithdrawalResponse>.Ok(
            result,
            "Withdrawal retry queued; worker will process automatically"));
    }

}
