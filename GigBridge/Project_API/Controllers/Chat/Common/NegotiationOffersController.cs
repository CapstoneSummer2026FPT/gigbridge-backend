using Application.Common.Models;
using Application.Features.Chat.Common.FinalOffers.Create.Commands;
using Application.Features.Chat.Common.FinalOffers.Create.DTOs;
using Application.Features.Chat.Common.FinalOffers.Get.DTOs;
using Application.Features.Chat.Common.FinalOffers.Get.Queries;
using Application.Features.Chat.Common.FinalOffers.Respond.Commands;
using Application.Features.Chat.Common.FinalOffers.Respond.DTOs;
using Application.Features.Chat.Common.Negotiations.MilestonePlans.Commands;
using Application.Features.Chat.Common.Negotiations.MilestonePlans.DTOs;
using Application.Features.Chat.Common.Negotiations.MilestonePlans.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Chat.Common;

[ApiController]
[Route("api/negotiation-offers")]
[Authorize]
public class NegotiationOffersController : BaseApiController
{
    [HttpGet("{offerId:guid}")]
    public async Task<IActionResult> GetFinalOffer(Guid offerId)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var result = await Mediator.Send(new GetNegotiationOfferDetailQuery(offerId, userId));
        return Ok(ApiResponse<NegotiationOfferDetailDto>.Ok(result, "Final offer retrieved"));
    }

    [HttpGet("conversations/{conversationId:guid}/milestone-plan")]
    public async Task<IActionResult> GetMilestonePlan(Guid conversationId)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var result = await Mediator.Send(new GetNegotiationMilestonePlanQuery(conversationId, userId));
        return Ok(ApiResponse<IReadOnlyCollection<NegotiationMilestoneDto>>.Ok(result, "Milestone plan retrieved"));
    }

    [HttpPut("conversations/{conversationId:guid}/milestone-plan")]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> UpdateMilestonePlan(
        Guid conversationId,
        [FromBody] UpdateNegotiationMilestonePlanRequest request)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var result = await Mediator.Send(new UpdateNegotiationMilestonePlanCommand(conversationId, userId, request));
        return Ok(ApiResponse<IReadOnlyCollection<NegotiationMilestoneDto>>.Ok(result, "Milestone plan updated"));
    }

    [HttpPost]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> CreateFinalOffer([FromBody] CreateFinalOfferRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var offerId = await Mediator.Send(new CreateFinalOfferCommand(userId, request));

        return Ok(ApiResponse<Guid>.Ok(offerId, "Final offer created"));
    }

    [HttpPost("respond")]
    [Authorize(Roles = "Freelancer")]
    public async Task<IActionResult> RespondFinalOffer([FromBody] RespondFinalOfferRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new RespondFinalOfferCommand(userId, request));

        return Ok(ApiResponse<RespondFinalOfferResponse>.Ok(result, "Final offer response recorded"));
    }
}
