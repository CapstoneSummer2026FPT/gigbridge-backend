using Application.Common.Exceptions;
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
using Application.Features.Contracts.Milestones.WorkItems.Client.Review.Commands;
using Application.Features.Contracts.Milestones.WorkItems.Common.DTOs;
using Application.Features.Contracts.Milestones.WorkItems.Freelancer.Submit.Commands;
using Application.Features.Contracts.WorkItems.Freelancer.Update.Commands;
using Application.Features.Contracts.WorkItems.Freelancer.Update.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Application.Common.Models.Files;

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

    /// <summary>
    /// Batch submission of work item deliverables. Multipart because each entry carries its own files:
    /// <c>submissionBatchId</c> plus an <c>items</c> JSON array of
    /// <c>{ workItemId, note, fileKeys[] }</c>, where every fileKey is the form field name of an
    /// uploaded file. The batch id is generated by the browser so a retry is idempotent server-side.
    /// </summary>
    [HttpPost("{milestoneId:guid}/work-items/submit")]
    [Authorize(Roles = "Freelancer")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestSizeBytes)]
    public async Task<IActionResult> SubmitWorkItems(
        Guid contractId,
        Guid milestoneId,
        [FromForm] Guid submissionBatchId,
        [FromForm] string items)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        if (Request.Form.Files.Count > WorkspaceUploadLimits.MaxFilesPerBatch)
        {
            throw new BadRequestException(
                $"A submission may contain at most {WorkspaceUploadLimits.MaxFilesPerBatch} files.");
        }

        var entries = ParseSubmitEntries(items);

        var streams = new List<Stream>();
        try
        {
            var commandItems = new List<SubmitWorkItemEntry>(entries.Count);
            foreach (var entry in entries)
            {
                var files = new List<WorkItemUploadFile>(entry.FileKeys.Count);
                foreach (var key in entry.FileKeys)
                {
                    var file = Request.Form.Files[key]
                        ?? throw new BadRequestException($"No uploaded file matches the key {key}.");

                    var stream = file.OpenReadStream();
                    streams.Add(stream);
                    files.Add(new WorkItemUploadFile(stream, file.FileName, file.ContentType, file.Length));
                }

                commandItems.Add(new SubmitWorkItemEntry(entry.WorkItemId, entry.Note, files));
            }

            var result = await Mediator.Send(new SubmitContractWorkItemsCommand(
                contractId, milestoneId, userId, submissionBatchId, commandItems));

            return Ok(ApiResponse<ContractMilestoneResponse>.Ok(result, "Work items submitted"));
        }
        finally
        {
            foreach (var stream in streams)
            {
                await stream.DisposeAsync();
            }
        }
    }

    [HttpPost("{milestoneId:guid}/work-items/approve")]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> ApproveWorkItems(
        Guid contractId,
        Guid milestoneId,
        [FromBody] ApproveWorkItemsRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new ReviewContractWorkItemsCommand(
            contractId, milestoneId, userId, request.WorkItemIds, Approve: true, Reason: null));

        return Ok(ApiResponse<ReviewWorkItemsResponse>.Ok(result, "Work items approved"));
    }

    [HttpPost("{milestoneId:guid}/work-items/request-revision")]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> RequestWorkItemRevision(
        Guid contractId,
        Guid milestoneId,
        [FromBody] RequestWorkItemRevisionRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new ReviewContractWorkItemsCommand(
            contractId, milestoneId, userId, request.WorkItemIds, Approve: false, request.Reason));

        return Ok(ApiResponse<ReviewWorkItemsResponse>.Ok(result, "Revision requested"));
    }

    private sealed record SubmitWorkItemEntryPayload(Guid WorkItemId, string? Note, List<string> FileKeys);

    private static List<SubmitWorkItemEntryPayload> ParseSubmitEntries(string items)
    {
        List<SubmitWorkItemEntryPayload>? entries;
        try
        {
            entries = System.Text.Json.JsonSerializer.Deserialize<List<SubmitWorkItemEntryPayload>>(
                items,
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        }
        catch (System.Text.Json.JsonException)
        {
            throw new BadRequestException("The submitted work item list is not valid JSON.");
        }

        if (entries is null || entries.Count == 0)
        {
            throw new BadRequestException("Select at least one work item to submit.");
        }

        return entries;
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
