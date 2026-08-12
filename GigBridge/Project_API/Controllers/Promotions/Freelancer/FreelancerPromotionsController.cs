using Application.Common.Models;
using Application.Features.Premium.Freelancer.Promotions.DTOs;
using Application.Features.Premium.Freelancer.Promotions.GetCurrent;
using Application.Features.Premium.Freelancer.Promotions.GetHistory;
using Application.Features.Premium.Freelancer.Promotions.Purchase;
using Application.Features.Premium.Freelancer.Promotions.Boost;
using Application.Features.Premium.Freelancer.Promotions.End;
using Application.Features.Premium.Freelancer.Promotions.GetDraft;
using Application.Features.Premium.Freelancer.Promotions.GetManager;
using Application.Features.Premium.Freelancer.Promotions.UploadPhoto;
using Domain.Enums.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Promotions.Freelancer;

[ApiController]
[Authorize(Roles = nameof(UserRole.Freelancer))]
[Route("api/freelancer/premium/promotions")]
public sealed class FreelancerPromotionsController : BaseApiController
{
    [HttpGet("draft")]
    public async Task<IActionResult> Draft(CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var id)) return InvalidTokenResponse();
        return Ok(ApiResponse<PromotionDraftDto>.Ok(await Mediator.Send(new GetPromotionDraftQuery(id), ct), "Success"));
    }

    [HttpGet("manager")]
    public async Task<IActionResult> Manager(CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var id)) return InvalidTokenResponse();
        return Ok(ApiResponse<PromotionManagerDto>.Ok(await Mediator.Send(new GetPromotionManagerQuery(id), ct), "Success"));
    }

    [HttpGet("current")]
    public async Task<IActionResult> Current(CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var id)) return InvalidTokenResponse();
        return Ok(ApiResponse<PromotionDto?>.Ok(
            await Mediator.Send(new GetCurrentPromotionQuery(id), ct), "Success"));
    }

    [HttpGet("history")]
    public async Task<IActionResult> History(CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var id)) return InvalidTokenResponse();
        return Ok(ApiResponse<IReadOnlyList<PromotionDto>>.Ok(
            await Mediator.Send(new GetPromotionHistoryQuery(id), ct), "Success"));
    }

    [HttpPost]
    public async Task<IActionResult> Purchase(
        [FromBody] PurchasePromotionRequest request, CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var id)) return InvalidTokenResponse();
        return Ok(ApiResponse<PromotionDto>.Ok(
            await Mediator.Send(new PurchasePromotionCommand(id, request), ct),
            "Promotion purchased"));
    }

    [HttpPost("{promotionId:guid}/boost")]
    public async Task<IActionResult> Boost(Guid promotionId, [FromBody] BoostPromotionRequest request, CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var id)) return InvalidTokenResponse();
        return Ok(ApiResponse<PromotionDto>.Ok(await Mediator.Send(new BoostPromotionCommand(id, promotionId, request), ct), "Promotion boosted"));
    }

    [HttpPost("{promotionId:guid}/end")]
    public async Task<IActionResult> End(Guid promotionId, CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var id)) return InvalidTokenResponse();
        return Ok(ApiResponse<PromotionDto>.Ok(
            await Mediator.Send(new EndPromotionCommand(id, promotionId), ct),
            "Promotion ended"));
    }

    [HttpPost("photo")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadPhoto(IFormFile file, CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var id)) return InvalidTokenResponse();
        await using var stream = file.OpenReadStream();
        var url = await Mediator.Send(new UploadPromotionPhotoCommand(id, stream, file.FileName, file.ContentType), ct);
        return Ok(ApiResponse<string>.Ok(url, "Photo uploaded"));
    }
}
