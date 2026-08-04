using Application.Common.Models;
using Application.Features.Wallets.Common.DTOs;
using Application.Features.Wallets.Common.Withdrawals.Admin;
using Application.Features.Wallets.Common.Withdrawals.Sync;
using Domain.Enums;
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
