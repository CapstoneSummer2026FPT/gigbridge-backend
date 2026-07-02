using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Features.Contracts.Milestones.Client.Approve.Commands;
using Application.Features.Contracts.Milestones.Client.RequestRevision.Commands;
using Application.Features.Contracts.Milestones.Client.Start.Commands;
using Application.Features.Contracts.Milestones.Common.DTOs;
using Application.Features.Contracts.Milestones.Common.List.Queries;
using Application.Features.Contracts.Milestones.Freelancer.RequestUnlock.Commands;
using Application.Features.Contracts.Milestones.Freelancer.Submit.Commands;
using Application.Features.Contracts.Milestones.Freelancer.Withdraw.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Common;

[ApiController]
[Route("api/contracts/{contractId:guid}/milestones")]
[Authorize]
public sealed class ContractMilestonesController : BaseApiController
{
    private const long MaxRequestSizeBytes = 100 * 1024 * 1024;

    [HttpGet]
    public async Task<IActionResult> GetMilestones(Guid contractId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GetContractMilestonesQuery(contractId, userId));

        return Ok(ApiResponse<IReadOnlyList<ContractMilestoneResponse>>.Ok(result, "Success"));
    }

    [HttpPost("{milestoneId:guid}/start")]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> Start(Guid contractId, Guid milestoneId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new StartMilestoneCommand(contractId, milestoneId, userId));

        return Ok(ApiResponse<ContractMilestoneResponse>.Ok(result, "Milestone started"));
    }

    [HttpPost("{milestoneId:guid}/submit")]
    [Authorize(Roles = "Freelancer")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestSizeBytes)]
    public async Task<IActionResult> Submit(
        Guid contractId,
        Guid milestoneId,
        [FromForm] string? externalUrl,
        [FromForm] string? description)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        if (Request.Form.Files.Count > 1)
        {
            throw new BadRequestException("Only one milestone file can be submitted at a time.");
        }

        SubmitMilestoneFile? commandFile = null;
        var file = Request.Form.Files.FirstOrDefault();
        if (file is not null)
        {
            commandFile = new SubmitMilestoneFile(
                file.OpenReadStream(),
                file.FileName,
                file.ContentType,
                file.Length);
        }

        var result = await Mediator.Send(
            new SubmitMilestoneCommand(contractId, milestoneId, userId, description, commandFile, externalUrl));

        return Ok(ApiResponse<ContractMilestoneResponse>.Ok(result, "Milestone submitted"));
    }

    [HttpPost("{milestoneId:guid}/request-unlock")]
    [Authorize(Roles = "Freelancer")]
    public async Task<IActionResult> RequestUnlock(Guid contractId, Guid milestoneId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        await Mediator.Send(new RequestMilestoneUnlockCommand(contractId, milestoneId, userId));

        return Ok(ApiResponse<object>.Ok(new { }, "Milestone unlock requested"));
    }

    [HttpPost("{milestoneId:guid}/approve")]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> Approve(Guid contractId, Guid milestoneId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new ApproveMilestoneCommand(contractId, milestoneId, userId));

        return Ok(ApiResponse<ContractMilestoneResponse>.Ok(result, "Milestone approved"));
    }

    [HttpPost("{milestoneId:guid}/request-revision")]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> RequestRevision(Guid contractId, Guid milestoneId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new RequestMilestoneRevisionCommand(contractId, milestoneId, userId));

        return Ok(ApiResponse<ContractMilestoneResponse>.Ok(result, "Milestone revision requested"));
    }

    [HttpPost("{milestoneId:guid}/withdraw")]
    [Authorize(Roles = "Freelancer")]
    public async Task<IActionResult> Withdraw(Guid contractId, Guid milestoneId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new WithdrawMilestoneCommand(contractId, milestoneId, userId));

        return Ok(ApiResponse<WithdrawMilestoneResponse>.Ok(result, "Milestone withdrawal released"));
    }
}
