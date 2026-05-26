using Application.DTOs.Admin;
using Application.Common.Models;
using Application.Features.Admin.Reviews.ChangeVisibility;
using Infrastructure.Services.Admin.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Admin;

[ApiController]
[Route("api/admin/reviews")]
public sealed class AdminReviewsController : AdminControllerBase
{
    private readonly IAdminReviewService _reviewService;

    public AdminReviewsController(IAdminReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] ReviewPageQueryDto query, CancellationToken cancellationToken)
    {
        var data = await _reviewService.GetAllAsync(query, cancellationToken);
        return Ok(ApiResponse<object>.Ok(data, "Reviews retrieved successfully"));
    }

    [HttpGet("{reviewId:guid}")]
    public async Task<IActionResult> Get(Guid reviewId, CancellationToken cancellationToken)
    {
        var data = await _reviewService.GetAsync(reviewId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(data, "Review retrieved successfully"));
    }

    [HttpPatch("{reviewId:guid}/visibility")]
    public async Task<IActionResult> SetVisibility(Guid reviewId, [FromBody] ReviewVisibilityRequestDto request, CancellationToken cancellationToken)
    {
        var data = await Mediator.Send(new ChangeReviewVisibilityCommand(reviewId, request, GetActor()), cancellationToken);
        return Ok(ApiResponse<object>.Ok(data, "Review visibility updated successfully"));
    }
}
