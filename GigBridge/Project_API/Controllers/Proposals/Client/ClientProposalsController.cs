using Application.Common.Models;
using Application.Common.Models.Ai;
using Application.Features.Proposals.Client.GetProposalJudgingList;
using Application.Features.Proposals.Client.GetProposalsByJobPost.Queries;
using Application.Features.Proposals.Client.JudgeAllProposals;
using Application.Features.Proposals.Common.DTOs;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Proposals.Client;

[ApiController]
[Route("api/Proposals")]
[Authorize(Roles = nameof(UserRole.Client))]
public class ClientProposalsController : BaseApiController
{
    [HttpGet("job/{jobPostId}/proposals")]
    public async Task<IActionResult> GetProposalsByJobPost(Guid jobPostId, [FromQuery] int pageIndex = 1, int pageSize = 10)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var query = new GetProposalsByJobPostQuery
        {
            JobPostsId = jobPostId,
            UserId = userId,
            PageIndex = pageIndex,
            PageSize = pageSize
        };

        var result = await Mediator.Send(query);
        return Ok(ApiResponse<IEnumerable<ProposalDto>>.Ok(result, "Success"));
    }

    [HttpGet("job/{jobPostId}/ai-judging-list")]
    public async Task<IActionResult> GetProposalJudgingList(
        Guid jobPostId,
        [FromQuery] bool? recommendedOnly,
        [FromQuery] int? minScore,
        [FromQuery] string? sortBy = "aiScore")
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var query = new GetProposalJudgingListQuery
        {
            JobPostId = jobPostId,
            UserId = userId,
            RecommendedOnly = recommendedOnly,
            MinScore = minScore,
            SortBy = sortBy
        };

        var result = await Mediator.Send(query);
        return Ok(ApiResponse<ProposalJudgingListDto>.Ok(result, "Success"));
    }

    [HttpPost("job/{jobPostId}/ai-judge-all")]
    public async Task<IActionResult> JudgeAllProposals(Guid jobPostId, [FromQuery] int batchSize = 10)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var command = new JudgeAllProposalsCommand
        {
            JobPostId = jobPostId,
            UserId = userId,
            BatchSize = batchSize
        };

        var result = await Mediator.Send(command);
        return Ok(ApiResponse<BatchJudgeResultDto>.Ok(result, "Success"));
    }
}
