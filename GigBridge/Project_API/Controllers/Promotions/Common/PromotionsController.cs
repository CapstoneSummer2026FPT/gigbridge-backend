using Application.Common.Models;
using Application.Features.Premium.Freelancer.Promotions.DTOs;
using Application.Features.Premium.Freelancer.Promotions.Feed;
using Application.Features.Premium.Freelancer.Promotions.Track;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Project_API.Controllers.Common;
using Project_API.Security;
namespace Project_API.Controllers;
[ApiController]
[AllowAnonymous]
[EnableRateLimiting(AuthRateLimitPolicies.PromotionTelemetry)]
[Route("api/promotions")]
public sealed class PromotionsController : BaseApiController
{
    [HttpGet("feed")]
    public async Task<IActionResult> Feed([FromQuery] int limit = 0, CancellationToken ct = default) =>
        Ok(ApiResponse<IReadOnlyList<PublicPromotionCardDto>>.Ok(await Mediator.Send(new GetPromotionFeedQuery(limit), ct), "Success"));

    [HttpPost("{promotionId:guid}/impression")]
    public Task<IActionResult> Impression(Guid promotionId, [FromHeader(Name = "X-Promotion-Visitor")] string visitor, CancellationToken ct) =>
        Track(promotionId, visitor, PromotionInteractionType.Impression, ct);

    [HttpPost("{promotionId:guid}/click")]
    public Task<IActionResult> Click(Guid promotionId, [FromHeader(Name = "X-Promotion-Visitor")] string visitor, CancellationToken ct) =>
        Track(promotionId, visitor, PromotionInteractionType.Click, ct);

    private async Task<IActionResult> Track(Guid id, string visitor, PromotionInteractionType type, CancellationToken ct) =>
        Ok(ApiResponse<PromotionInteractionResultDto>.Ok(await Mediator.Send(new TrackPromotionInteractionCommand(id, visitor, type), ct), "Recorded"));
}
