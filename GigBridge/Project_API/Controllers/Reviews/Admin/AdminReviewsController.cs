using Application.Common.Models;
using Application.Features.Reviews.Admin.DTOs;
using Application.Features.Reviews.Admin.GetReviews.Queries;
using Application.Features.Reviews.Admin.ModerateReview.Commands;
using Application.Features.Reviews.Common.DTOs;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Reviews.Admin;

[ApiController]
[Route("api/Reviews/admin")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminReviewsController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetReviews(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] int? rating = null,
        [FromQuery] int? reviewerRole = null,
        [FromQuery] int? revieweeRole = null,
        [FromQuery] ReviewModerationStatus? moderationStatus = null,
        [FromQuery] bool? hasOpenReport = null)
    {
        var result = await Mediator.Send(new GetAdminReviewsQuery(
            page,
            pageSize,
            search,
            rating,
            reviewerRole,
            revieweeRole,
            moderationStatus,
            hasOpenReport));
        return Ok(ApiResponse<AdminReviewsResponse>.Ok(result, "Reviews retrieved successfully"));
    }

    [HttpPut("{reviewId:guid}/moderation")]
    public async Task<IActionResult> ModerateReview(Guid reviewId, [FromBody] ModerateReviewRequest request)
    {
        if (!TryGetCurrentUserId(out var adminId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new ModerateReviewCommand(reviewId, adminId, request));
        return Ok(ApiResponse<ManagedReviewDto>.Ok(result, "Review moderation updated successfully"));
    }
}
