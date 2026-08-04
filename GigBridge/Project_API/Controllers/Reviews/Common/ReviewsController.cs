using Application.Common.Models;
using Application.Features.Reviews.Common.CreateReview.Commands;
using Application.Features.Reviews.Common.DTOs;
using Application.Features.Reviews.Common.GetReviewsByUser.Queries;
using Application.Features.Reviews.Common.GetReviewStats.Queries;
using Application.Features.Reviews.Common.GetMyReviews.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Reviews.Common;

[ApiController]
[Route("api/Reviews")]
[Authorize]
public class ReviewsController : BaseApiController
{
    [HttpGet("my")]
    public async Task<IActionResult> GetMyReviews(
        [FromQuery] string direction = "received",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GetMyReviewsQuery(userId, direction, page, pageSize));
        return Ok(ApiResponse<MyReviewsResponse>.Ok(result, "Reviews retrieved successfully"));
    }

    [HttpPost]
    public async Task<IActionResult> CreateReview([FromBody] CreateReviewRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new CreateReviewCommand(userId, request));

        return Ok(ApiResponse<ReviewDto>.Ok(result, "Review submitted successfully"));
    }

    [HttpGet("user/{userId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetReviewsByUser(Guid userId)
    {
        var result = await Mediator.Send(new GetReviewsByUserQuery(userId));

        return Ok(ApiResponse<IEnumerable<ReviewDto>>.Ok(result, "Success"));
    }

    [HttpGet("user/{userId}/stats")]
    [AllowAnonymous]
    public async Task<IActionResult> GetReviewStats(Guid userId)
    {
        var result = await Mediator.Send(new GetReviewStatsQuery(userId));

        return Ok(ApiResponse<ReviewStatsDto>.Ok(result, "Success"));
    }
}
