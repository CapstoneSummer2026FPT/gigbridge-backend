using Application.Common.Models;
using Application.Features.Premium.Client.JobPostPromotion.Commands;
using Application.Features.Premium.Client.JobPostPromotion.DTOs;
using Application.Features.Premium.Client.JobPostPromotion.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/job-promotions")]
public sealed class JobPromotionsController : BaseApiController
{
    [HttpGet("feed")]
    public async Task<IActionResult> Feed([FromQuery] int limit = 10, CancellationToken cancellationToken = default) =>
        Ok(ApiResponse<IReadOnlyList<PublicJobPromotionCardDto>>.Ok(
            await Mediator.Send(new GetJobPromotionFeedQuery(limit), cancellationToken), "Success"));

    [HttpPost("{promotionId:guid}/impression")]
    public Task<IActionResult> Impression(Guid promotionId, CancellationToken cancellationToken) =>
        Track(promotionId, JobPromotionInteractionType.Impression, cancellationToken);

    [HttpPost("{promotionId:guid}/click")]
    public Task<IActionResult> Click(Guid promotionId, CancellationToken cancellationToken) =>
        Track(promotionId, JobPromotionInteractionType.Click, cancellationToken);

    private async Task<IActionResult> Track(
        Guid promotionId,
        JobPromotionInteractionType type,
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<JobPromotionInteractionDto>.Ok(
            await Mediator.Send(new TrackJobPromotionCommand(promotionId, type), cancellationToken), "Recorded"));
}
