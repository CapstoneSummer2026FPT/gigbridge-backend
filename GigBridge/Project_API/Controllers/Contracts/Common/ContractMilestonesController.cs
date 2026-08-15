using Application.Common.Exceptions;
using Application.Common.Interfaces.Files;
using Application.Common.Models;
using Application.Features.Contracts.Milestones.Client.Approve.Commands;
using Application.Features.Contracts.Milestones.Client.RequestRevision.Commands;
using Application.Features.Contracts.Milestones.Client.RequestRevision.DTOs;
using Application.Features.Contracts.Milestones.Client.Start.Commands;
using Application.Features.Contracts.Milestones.Client.RespondEarlyStart.Commands;
using Application.Features.Contracts.Milestones.Client.RespondEarlyStart.DTOs;
using Application.Features.Contracts.Milestones.Common.DTOs;
using Application.Features.Contracts.Milestones.Common.Get.Queries;
using Application.Features.Contracts.Milestones.Common.List.Queries;
using Application.Features.Contracts.Milestones.Common.EarlyStartRequests.Queries;
using Application.Features.Contracts.Milestones.Freelancer.RequestUnlock.Commands;
using Application.Features.Contracts.Milestones.Freelancer.RequestUnlock.DTOs;
using Application.Features.Contracts.Milestones.Freelancer.Submit.Commands;
using Application.Features.Contracts.Milestones.Freelancer.Withdraw.Commands;
using Application.Features.Contracts.WorkItems.Freelancer.Update.Commands;
using Application.Features.Contracts.WorkItems.Freelancer.Update.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Contracts.Common;

[ApiController]
[Route("api/contracts/{contractId:guid}/milestones")]
[Authorize]
public sealed class ContractMilestonesController : BaseApiController
{
    private const long MaxRequestSizeBytes =
        WorkspaceUploadLimits.MaxTotalFileSizeBytes +
        WorkspaceUploadLimits.MultipartOverheadAllowanceBytes;

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

    [HttpGet("{milestoneId:guid}")]
    public async Task<IActionResult> GetMilestone(Guid contractId, Guid milestoneId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GetMilestoneByIdQuery(milestoneId, userId, contractId));

        return Ok(ApiResponse<ContractMilestoneResponse>.Ok(result, "Success"));
    }

    [HttpGet("{milestoneId:guid}/attachments")]
    public async Task<IActionResult> GetAttachments(Guid contractId, Guid milestoneId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var milestone = await Mediator.Send(new GetMilestoneByIdQuery(milestoneId, userId, contractId));

        return Ok(ApiResponse<IReadOnlyList<MilestoneAttachmentResponse>>.Ok(
            milestone.Attachments,
            "Success"));
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

    [HttpGet("early-start-requests")]
    public async Task<IActionResult> GetEarlyStartRequests(Guid contractId)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var result = await Mediator.Send(new GetMilestoneEarlyStartRequestsQuery(contractId, userId));
        return Ok(ApiResponse<IReadOnlyList<MilestoneEarlyStartRequestDto>>.Ok(result, "Success"));
    }

    [HttpPatch("{milestoneId:guid}/work-items/{workItemId:guid}")]
    [Authorize(Roles = "Freelancer")]
    public async Task<IActionResult> UpdateWorkItem(
        Guid contractId,
        Guid milestoneId,
        Guid workItemId,
        [FromBody] UpdateContractWorkItemRequest request)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var result = await Mediator.Send(new UpdateContractWorkItemCommand(contractId, milestoneId, workItemId, userId, request));
        return Ok(ApiResponse<ContractWorkItemResponse>.Ok(result, "Work item updated"));
    }

    [HttpPost("{milestoneId:guid}/submit")]
    [Authorize(Roles = "Freelancer")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestSizeBytes)]
    public async Task<IActionResult> Submit(
        Guid contractId,
        Guid milestoneId,
        [FromForm] string? description)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        if (Request.Form.Files.Count > WorkspaceUploadLimits.MaxFilesPerBatch)
        {
            throw new BadRequestException(
                $"A milestone submission may contain at most {WorkspaceUploadLimits.MaxFilesPerBatch} files.");
        }

        var streams = new List<Stream>();
        try
        {
            var commandFiles = new List<SubmitMilestoneFile>(Request.Form.Files.Count);
            foreach (var file in Request.Form.Files)
            {
                var stream = file.OpenReadStream();
                streams.Add(stream);
                commandFiles.Add(new SubmitMilestoneFile(
                    stream,
                    file.FileName,
                    file.ContentType,
                    file.Length));
            }

            var result = await Mediator.Send(
                new SubmitMilestoneCommand(
                    contractId,
                    milestoneId,
                    userId,
                    description,
                    commandFiles));

            return Ok(ApiResponse<ContractMilestoneResponse>.Ok(result, "Milestone submitted"));
        }
        finally
        {
            foreach (var stream in streams)
            {
                await stream.DisposeAsync();
            }
        }
    }

    [HttpPost("{milestoneId:guid}/early-start-requests")]
    [Authorize(Roles = "Freelancer")]
    public async Task<IActionResult> RequestUnlock(
        Guid contractId,
        Guid milestoneId,
        [FromBody] RequestMilestoneEarlyStartRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        await Mediator.Send(new RequestMilestoneUnlockCommand(contractId, milestoneId, userId, request.Reason));

        return Ok(ApiResponse<object>.Ok(new { }, "Milestone early start requested"));
    }

    [HttpPost("early-start-requests/{requestId:guid}/respond")]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> RespondEarlyStart(
        Guid contractId,
        Guid requestId,
        [FromBody] RespondMilestoneEarlyStartRequest request)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var result = await Mediator.Send(new RespondMilestoneEarlyStartCommand(contractId, requestId, userId, request));
        return Ok(ApiResponse<MilestoneEarlyStartRequestDto>.Ok(result, "Early start request answered"));
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
    public async Task<IActionResult> RequestRevision(
        Guid contractId,
        Guid milestoneId,
        [FromBody] RequestMilestoneRevisionRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new RequestMilestoneRevisionCommand(contractId, milestoneId, userId, request));

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

        return Ok(ApiResponse<WithdrawMilestoneResponse>.Ok(result, "Milestone early withdrawal released"));
    }

}
