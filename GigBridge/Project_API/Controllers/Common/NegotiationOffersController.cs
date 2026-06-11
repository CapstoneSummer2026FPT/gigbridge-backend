using Application.Common.Models;
using Application.Features.Chat.FinalOffers.Create.Commands;
using Application.Features.Chat.FinalOffers.Create.DTOs;
using Application.Features.Chat.FinalOffers.Respond.Commands;
using Application.Features.Chat.FinalOffers.Respond.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Common;

[ApiController]
[Route("api/negotiation-offers")]
[Authorize]
public class NegotiationOffersController : BaseApiController
{
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

        return Ok(ApiResponse<bool>.Ok(result, "Final offer response recorded"));
    }
}
