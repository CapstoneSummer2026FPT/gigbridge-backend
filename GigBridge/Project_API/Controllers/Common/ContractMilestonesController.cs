using Application.Common.Models;
using Application.Features.Contracts.Milestones.Client.Approve.Commands;
using Application.Features.Contracts.Milestones.Client.RequestRevision.Commands;
using Application.Features.Contracts.Milestones.Client.Start.Commands;
using Application.Features.Contracts.Milestones.Common.DTOs;
using Application.Features.Contracts.Milestones.Common.List.Queries;
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
    public async Task<IActionResult> Submit(Guid contractId, Guid milestoneId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new SubmitMilestoneCommand(contractId, milestoneId, userId));

        return Ok(ApiResponse<ContractMilestoneResponse>.Ok(result, "Milestone submitted"));
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
