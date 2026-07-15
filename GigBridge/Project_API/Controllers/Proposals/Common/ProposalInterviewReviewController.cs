using Application.Common.Models;
using Application.Features.Proposals.Freelancer.InterviewReview.Commands;
using Application.Features.Proposals.Freelancer.InterviewReview.DTOs;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Common.Proposals;

[ApiController]
[Route("api/Proposals/{proposalId:guid}/interview-review")]
[Authorize(Roles = nameof(UserRole.Freelancer))]
public class ProposalInterviewReviewController : BaseApiController
{
    [HttpPost("start")]
    public async Task<IActionResult> StartReview(Guid proposalId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new StartInterviewReviewCommand(proposalId, userId));
        return Ok(ApiResponse<InterviewReviewSessionDto>.Ok(result, "Interview review started successfully"));
    }

    [HttpPost("complete")]
    public async Task<IActionResult> CompleteReview(Guid proposalId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new CompleteInterviewReviewCommand(proposalId, userId));
        return Ok(ApiResponse<InterviewReviewSessionDto>.Ok(result, "Interview review completed successfully"));
    }
}
