using Application.Common.Models;
using Application.Features.Contracts.Amendments.Commands;
using Application.Features.Contracts.Amendments.DTOs;
using Application.Features.Contracts.Amendments.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Contracts.Common;

[ApiController]
[Route("api/Contracts/{contractId:guid}")]
[Authorize]
public sealed class ContractAmendmentsController : BaseApiController
{
    [HttpGet("change-requests")]
    public async Task<IActionResult> GetChangeRequests(Guid contractId)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var result = await Mediator.Send(new GetContractChangeRequestsQuery(contractId, userId));
        return Ok(ApiResponse<IReadOnlyList<ContractChangeRequestDto>>.Ok(result, "Success"));
    }

    [HttpPost("change-requests")]
    public async Task<IActionResult> CreateChangeRequest(Guid contractId, [FromBody] CreateContractChangeRequest request)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var id = await Mediator.Send(new CreateContractChangeRequestCommand(contractId, userId, request));
        return Ok(ApiResponse<Guid>.Ok(id, "Change request created"));
    }

    [HttpPost("change-requests/{requestId:guid}/respond")]
    public async Task<IActionResult> RespondChangeRequest(Guid contractId, Guid requestId, [FromBody] RespondContractChangeRequest request)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        await Mediator.Send(new RespondContractChangeRequestCommand(contractId, requestId, userId, request));
        return Ok(ApiResponse<bool>.Ok(true, "Change request updated"));
    }

    [HttpGet("amendments")]
    public async Task<IActionResult> GetAmendments(Guid contractId)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var result = await Mediator.Send(new GetContractAmendmentsQuery(contractId, userId));
        return Ok(ApiResponse<IReadOnlyList<ContractAmendmentDetailDto>>.Ok(result, "Success"));
    }

    [HttpGet("amendments/{amendmentId:guid}")]
    public async Task<IActionResult> GetAmendment(Guid contractId, Guid amendmentId)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var result = await Mediator.Send(new GetContractAmendmentDetailQuery(contractId, amendmentId, userId));
        return Ok(ApiResponse<ContractAmendmentDetailDto>.Ok(result, "Success"));
    }

    [HttpPost("amendments")]
    public async Task<IActionResult> CreateAmendment(Guid contractId, [FromBody] CreateContractAmendmentRequest request)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var id = await Mediator.Send(new CreateContractAmendmentCommand(contractId, userId, request));
        return Ok(ApiResponse<Guid>.Ok(id, "Amendment submitted for review"));
    }

    [HttpPut("amendments/{amendmentId:guid}")]
    public async Task<IActionResult> UpdateAmendment(Guid contractId, Guid amendmentId, [FromBody] CreateContractAmendmentRequest request)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        await Mediator.Send(new UpdateContractAmendmentCommand(contractId, amendmentId, userId, request));
        return Ok(ApiResponse<bool>.Ok(true, "Amendment resubmitted"));
    }

    [HttpPost("amendments/{amendmentId:guid}/respond")]
    public async Task<IActionResult> RespondAmendment(Guid contractId, Guid amendmentId, [FromBody] RespondContractAmendmentRequest request)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        await Mediator.Send(new RespondContractAmendmentCommand(contractId, amendmentId, userId, request));
        return Ok(ApiResponse<bool>.Ok(true, "Amendment response saved"));
    }

    [HttpPost("amendments/{amendmentId:guid}/sign")]
    public async Task<IActionResult> SignAmendment(Guid contractId, Guid amendmentId, [FromBody] SignContractAmendmentRequest request)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        await Mediator.Send(new SignContractAmendmentCommand(contractId, amendmentId, userId, request));
        return Ok(ApiResponse<bool>.Ok(true, "Amendment signed"));
    }

    [HttpPost("amendments/{amendmentId:guid}/fund")]
    public async Task<IActionResult> FundAmendment(Guid contractId, Guid amendmentId)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        await Mediator.Send(new FundContractAmendmentCommand(contractId, amendmentId, userId));
        return Ok(ApiResponse<bool>.Ok(true, "Amendment funded and applied"));
    }
}
