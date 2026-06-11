using Application.Common.Models;
using Application.Features.Contracts.Common.GetContractByJobPost.DTOs;
using Application.Features.Contracts.Common.GetContractByJobPost.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Common;

[ApiController]
[Route("api/Contracts")]
[Authorize]
public class ContractsController : BaseApiController
{
    [HttpGet("job/{jobPostId}")]
    public async Task<IActionResult> GetContractByJobPost(Guid jobPostId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GetContractByJobPostQuery(jobPostId, userId));

        return Ok(ApiResponse<ContractDetailResponse>.Ok(result, "Success"));
    }
}
