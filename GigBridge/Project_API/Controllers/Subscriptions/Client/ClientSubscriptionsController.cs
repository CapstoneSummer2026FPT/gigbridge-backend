using Application.Common.Models;
using Application.Features.Premium.Client.Subscriptions.GetCurrent;
using Application.Features.Premium.Client.Subscriptions.GetHistory;
using Application.Features.Premium.Client.Subscriptions.GetPlans;
using Application.Features.Premium.Client.Subscriptions.Purchase;
using Application.Features.Premium.Client.Subscriptions.Cancel;
using Application.Features.Premium.Client.Subscriptions.AutoRenew.Commands;
using Application.Features.Premium.Client.Subscriptions.AutoRenew.DTOs;
using Application.Features.Subscriptions.Freelancer.DTOs;
using Application.Features.Subscriptions.Freelancer.Purchase;
using Domain.Enums.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Subscriptions.Client;

[ApiController]
[Authorize(Roles = nameof(UserRole.Client))]
[Route("api/client/subscriptions")]
public sealed class ClientSubscriptionsController : BaseApiController
{
    [HttpGet("plans")]
    public async Task<IActionResult> GetPlans(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetClientSubscriptionPlansQuery(), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<SubscriptionPlanDto>>.Ok(result, "Success"));
    }

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var result = await Mediator.Send(new GetCurrentClientSubscriptionQuery(userId), cancellationToken);
        return Ok(ApiResponse<SubscriptionDto?>.Ok(result, "Success"));
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var result = await Mediator.Send(
            new GetClientSubscriptionHistoryQuery(userId), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<SubscriptionDto>>.Ok(result, "Success"));
    }

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var result = await Mediator.Send(
            new CancelClientSubscriptionCommand(userId), cancellationToken);
        return Ok(ApiResponse<SubscriptionDto>.Ok(result, "Client Premium renewal cancelled"));
    }

    [HttpPut("auto-renew")]
    public async Task<IActionResult> UpdateAutoRenew(
        [FromBody] UpdateClientSubscriptionAutoRenewRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var result = await Mediator.Send(
            new UpdateClientSubscriptionAutoRenewCommand(userId, request.AutoRenew),
            cancellationToken);
        return Ok(ApiResponse<SubscriptionDto>.Ok(
            result, "Client Premium auto-renewal updated"));
    }

    [HttpPost("purchase")]
    public async Task<IActionResult> Purchase(
        [FromBody] PurchaseSubscriptionRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var result = await Mediator.Send(new PurchaseClientSubscriptionCommand(userId, request), cancellationToken);
        return Ok(ApiResponse<SubscriptionDto>.Ok(result, "Client Premium activated"));
    }
}
