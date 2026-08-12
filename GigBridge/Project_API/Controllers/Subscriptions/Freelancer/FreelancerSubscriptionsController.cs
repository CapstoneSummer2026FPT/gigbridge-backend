using Application.Common.Models;
using Application.Features.Subscriptions.Freelancer.DTOs;
using Application.Features.Subscriptions.Freelancer.Cancel;
using Application.Features.Subscriptions.Freelancer.GetCurrent;
using Application.Features.Subscriptions.Freelancer.GetHistory;
using Application.Features.Subscriptions.Freelancer.GetPlans;
using Application.Features.Subscriptions.Freelancer.Purchase;
using Application.Features.Premium.Freelancer.AutoRenew.Commands;
using Application.Features.Premium.Freelancer.AutoRenew.DTOs;
using Domain.Enums.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Subscriptions.Freelancer;

[ApiController]
[Authorize(Roles = nameof(UserRole.Freelancer))]
[Route("api/freelancer/subscriptions")]
public sealed class FreelancerSubscriptionsController : BaseApiController
{
    [HttpGet("plans")]
    public async Task<IActionResult> GetPlans(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetSubscriptionPlansQuery(), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<SubscriptionPlanDto>>.Ok(result, "Success"));
    }

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return InvalidTokenResponse();

        var result = await Mediator.Send(new GetCurrentSubscriptionQuery(userId), cancellationToken);
        return Ok(ApiResponse<SubscriptionDto?>.Ok(result, "Success"));
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return InvalidTokenResponse();

        var result = await Mediator.Send(new GetSubscriptionHistoryQuery(userId), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<SubscriptionDto>>.Ok(result, "Success"));
    }

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return InvalidTokenResponse();
        var result = await Mediator.Send(new CancelSubscriptionCommand(userId), cancellationToken);
        return Ok(ApiResponse<SubscriptionDto>.Ok(result, "Subscription renewal cancelled"));
    }

    [HttpPut("auto-renew")]
    public async Task<IActionResult> UpdateAutoRenew(
        [FromBody] UpdateSubscriptionAutoRenewRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return InvalidTokenResponse();

        var result = await Mediator.Send(
            new UpdateSubscriptionAutoRenewCommand(userId, request.AutoRenew), cancellationToken);
        return Ok(ApiResponse<SubscriptionDto>.Ok(result, "Subscription auto-renewal updated"));
    }

    [HttpPost("purchase")]
    public async Task<IActionResult> Purchase(
        [FromBody] PurchaseSubscriptionRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var result = await Mediator.Send(new PurchaseSubscriptionCommand(userId, request), cancellationToken);
        return Ok(ApiResponse<SubscriptionDto>.Ok(result, "Freelancer Premium activated"));
    }
}
