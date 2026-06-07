using Application.Common.Models;
using Application.Features.Proposals.Client.GetProposalsByJobPost.Queries;
using Application.Features.Proposals.Common.DTOs;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Client;

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
}
