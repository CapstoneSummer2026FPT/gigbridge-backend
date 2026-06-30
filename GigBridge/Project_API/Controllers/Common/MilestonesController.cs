using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Features.Contracts.Milestones.Client.Approve.Commands;
using Application.Features.Contracts.Milestones.Client.RequestRevision.Commands;
using Application.Features.Contracts.Milestones.Client.Start.Commands;
using Application.Features.Contracts.Milestones.Common.DTOs;
using Application.Features.Contracts.Milestones.Common.Get.Queries;
using Application.Features.Contracts.Milestones.Common.List.Queries;
using Application.Features.Contracts.Milestones.Freelancer.Submit.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Common;

[ApiController]
[Route("api/Milestones")]
[Authorize]
public sealed class MilestonesController : BaseApiController
{
    private const long MaxRequestSizeBytes = 100 * 1024 * 1024;

    [HttpGet("contract/{contractId:guid}")]
    public async Task<IActionResult> GetMilestonesByContract(Guid contractId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GetContractMilestonesQuery(contractId, userId));

        return Ok(ApiResponse<IReadOnlyList<ContractMilestoneResponse>>.Ok(result, "Success"));
    }

    [HttpGet("{milestoneId:guid}")]
    public async Task<IActionResult> GetMilestoneById(Guid milestoneId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GetMilestoneByIdQuery(milestoneId, userId));

        return Ok(ApiResponse<ContractMilestoneResponse>.Ok(result, "Success"));
    }

    [HttpGet("{milestoneId:guid}/attachments")]
    public async Task<IActionResult> GetAttachments(Guid milestoneId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GetMilestoneByIdQuery(milestoneId, userId));

        return Ok(ApiResponse<IReadOnlyList<MilestoneAttachmentResponse>>.Ok(result.Attachments, "Success"));
    }

    [HttpPost("{milestoneId:guid}/submit-deliverables")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestSizeBytes)]
    public async Task<IActionResult> SubmitDeliverables(
        Guid milestoneId,
        [FromForm] string? description,
        [FromForm] string? externalUrl)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var milestone = await Mediator.Send(new GetMilestoneByIdQuery(milestoneId, userId));
        var contractId = milestone.ContractId;

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

        var result = await Mediator.Send(new SubmitMilestoneCommand(
            contractId,
            milestoneId,
            userId,
            description,
            commandFile,
            externalUrl));

        return Ok(ApiResponse<ContractMilestoneResponse>.Ok(result, "Milestone deliverables submitted"));
    }

    public record UpdateMilestoneStatusRequest(int Status);

    [HttpPut("{milestoneId:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid milestoneId, [FromBody] UpdateMilestoneStatusRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var milestone = await Mediator.Send(new GetMilestoneByIdQuery(milestoneId, userId));
        var contractId = milestone.ContractId;

        // Map frontend statuses: InProgress = 4, Paid = 2 (Approve), RevisionRequired = 6
        if (request.Status == 4) // InProgress -> Start milestone
        {
            var result = await Mediator.Send(new StartMilestoneCommand(contractId, milestoneId, userId));
            return Ok(ApiResponse<ContractMilestoneResponse>.Ok(result, "Milestone started"));
        }
        else if (request.Status == 2) // Paid -> Approve milestone
        {
            var result = await Mediator.Send(new ApproveMilestoneCommand(contractId, milestoneId, userId));
            return Ok(ApiResponse<ContractMilestoneResponse>.Ok(result, "Milestone approved"));
        }
        else if (request.Status == 6) // RevisionRequired -> Request Revision
        {
            var result = await Mediator.Send(new RequestMilestoneRevisionCommand(contractId, milestoneId, userId));
            return Ok(ApiResponse<ContractMilestoneResponse>.Ok(result, "Revision requested"));
        }

        return BadRequest(ApiResponse<object>.BadRequest("Invalid milestone status transition."));
    }
}
