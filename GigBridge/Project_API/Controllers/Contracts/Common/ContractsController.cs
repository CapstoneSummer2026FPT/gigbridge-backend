using Application.Common.Models;
using Application.Features.Contracts.Common.GetContractByJobPost.DTOs;
using Application.Features.Contracts.Common.GetContractByJobPost.Queries;
using Application.Features.Contracts.Common.GetMyContracts.DTOs;
using Application.Features.Contracts.Common.GetMyContracts.Queries;
using Application.Features.Contracts.Common.GetContractById.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Contracts.Common;

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

    [HttpGet("my-contracts")]
    public async Task<IActionResult> GetMyContracts([FromQuery] int? status)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GetMyContractsQuery(userId, status));

        return Ok(ApiResponse<List<ContractDtoResponse>>.Ok(result, "Success"));
    }

    [HttpGet("{contractId:guid}")]
    public async Task<IActionResult> GetContractById(Guid contractId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GetContractByIdQuery(contractId, userId));

        return Ok(ApiResponse<ContractDetailResponse>.Ok(result, "Success"));
    }
}

