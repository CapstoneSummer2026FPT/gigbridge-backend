using Application.Common.Models;
using Application.Features.Admin.Disputes.Common.DTOs;
using Application.Features.Admin.Disputes.DownloadEvidence.Queries;
using Application.Features.Admin.Disputes.GetDetail.Queries;
using Application.Features.Admin.Disputes.GetList.Queries;
using Application.Features.Admin.Disputes.RequestEvidence.Commands;
using Application.Features.Admin.Disputes.ReviewEvidence.Commands;
using Application.Features.Admin.Disputes.Resolve.Commands;
using Application.Features.Admin.Disputes.SendMessage.Commands;
using Application.Features.Admin.Disputes.UpdateStatus.Commands;
using Application.Features.Disputes.Common.DTOs;
using Application.Features.Chat.Common.Messages.GetConversationMessages.DTOs;
using Application.Features.Chat.Common.Messages.GetConversationMessages.Queries;
using Application.Features.Chat.Common.Messages.Send.DTOs;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Admin.Disputes;

public sealed record AdminDisputeStatusRequest(DisputeStatus Status);

public sealed record AdminMilestoneDecisionRequest(
    Guid MilestoneId,
    DisputeMilestoneOutcome Outcome,
    decimal AdditionalReleaseToFreelancer,
    decimal RefundToClient);

public sealed record AdminMilestoneAllocationRequest(
    Guid MilestoneId,
    DisputeMilestoneOutcome Outcome,
    decimal FreelancerAward,
    decimal ClientRefund,
    decimal PenaltyAmount,
    string? Reason);

public sealed record AdminViolationRequest(
    bool IsViolation,
    UserViolationType? ViolationType,
    string? Reason,
    string? Description);

public sealed record AdminResolveDisputeRequest(
    DisputeResolution Resolution,
    string ResolutionNote,
    string? InternalNotes,
    List<AdminMilestoneAllocationRequest>? MilestoneAllocations,
    List<AdminMilestoneDecisionRequest>? MilestoneDecisions,
    int ContractAction,
    AdminViolationRequest? ClientViolation,
    AdminViolationRequest? FreelancerViolation);

public sealed record AdminRequestEvidenceRequest(
    string Reason,
    DateTime? Deadline,
    EvidenceRequestTarget Target);

public sealed record AdminReviewEvidenceRequest(string? ReviewNote);

[ApiController]
[Route("api/admin/disputes")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminDisputesController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DisputeStatus? status = null,
        [FromQuery] string? search = null)
    {
        var result = await Mediator.Send(new GetAdminDisputesQuery(
            page,
            pageSize,
            status,
            search));

        return Ok(ApiResponse<AdminDisputeListResponse>.Ok(result, "Disputes retrieved successfully."));
    }

    [HttpGet("{disputeId:guid}")]
    public async Task<IActionResult> GetById(Guid disputeId)
    {
        var result = await Mediator.Send(new GetAdminDisputeDetailQuery(disputeId));
        return Ok(ApiResponse<AdminDisputeDetailResponse>.Ok(result, "Dispute retrieved successfully."));
    }

    [HttpGet("{disputeId:guid}/evidence/{evidenceId:guid}/download")]
    public async Task<IActionResult> DownloadEvidence(Guid disputeId, Guid evidenceId)
    {
        if (!TryGetCurrentUserId(out var adminId))
            return InvalidTokenResponse();

        var result = await Mediator.Send(new GetAdminDisputeEvidenceDownloadQuery(
            disputeId,
            evidenceId,
            adminId));

        return Ok(ApiResponse<DisputeEvidenceDownloadResponse>.Ok(result, "Success"));
    }

    [HttpGet("{disputeId:guid}/conversations/{conversationId:guid}/messages")]
    public async Task<IActionResult> GetConversationMessages(
        Guid disputeId,
        Guid conversationId,
        [FromQuery] DateTime? before,
        [FromQuery] int pageSize = 50)
    {
        if (!TryGetCurrentUserId(out var adminId))
            return InvalidTokenResponse();

        var result = await Mediator.Send(new GetConversationMessagesQuery(
            conversationId,
            adminId,
            before,
            pageSize,
            disputeId));
        return Ok(ApiResponse<IReadOnlyList<ConversationMessageResponse>>.Ok(result, "Success"));
    }

    [HttpPost("{disputeId:guid}/conversations/{conversationId:guid}/messages")]
    [RequestSizeLimit(502 * 1024 * 1024)]
    public async Task<IActionResult> SendDisputeMessage(
        Guid disputeId,
        Guid conversationId,
        [FromForm] string? content,
        [FromForm] List<IFormFile>? attachments)
    {
        if (!TryGetCurrentUserId(out var adminId))
            return InvalidTokenResponse();

        var streams = new List<Stream>();
        try
        {
            var files = new List<AdminDisputeMessageFile>();
            foreach (var file in attachments ?? [])
            {
                var stream = file.OpenReadStream();
                streams.Add(stream);
                files.Add(new AdminDisputeMessageFile(stream, file.FileName, file.ContentType, file.Length));
            }

            var result = await Mediator.Send(new SendAdminDisputeMessageCommand(
                disputeId,
                conversationId,
                adminId,
                content,
                files));
            return Ok(ApiResponse<MessageResponse>.Ok(result, "Official message sent successfully."));
        }
        finally
        {
            foreach (var stream in streams)
                await stream.DisposeAsync();
        }
    }

    [HttpPost("{disputeId:guid}/evidence/{evidenceId:guid}/review")]
    public async Task<IActionResult> ReviewEvidence(
        Guid disputeId,
        Guid evidenceId,
        [FromBody] AdminReviewEvidenceRequest request)
    {
        if (!TryGetCurrentUserId(out var adminId))
            return InvalidTokenResponse();

        var result = await Mediator.Send(new ReviewDisputeEvidenceCommand(
            disputeId,
            evidenceId,
            adminId,
            request.ReviewNote));
        return Ok(ApiResponse<DisputeEvidenceResponse>.Ok(result, "Evidence reviewed successfully."));
    }

    [HttpPatch("{disputeId:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid disputeId,
        [FromBody] AdminDisputeStatusRequest request)
    {
        if (!TryGetCurrentUserId(out var adminId))
            return InvalidTokenResponse();

        var result = await Mediator.Send(new UpdateAdminDisputeStatusCommand(
            disputeId,
            adminId,
            request.Status));

        return Ok(ApiResponse<AdminDisputeDetailResponse>.Ok(result, "Dispute status updated successfully."));
    }

    [HttpPost("{disputeId:guid}/request-evidence")]
    public async Task<IActionResult> RequestEvidence(
        Guid disputeId,
        [FromBody] AdminRequestEvidenceRequest request)
    {
        if (!TryGetCurrentUserId(out var adminId))
            return InvalidTokenResponse();

        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new Application.Common.Exceptions.BadRequestException("Evidence request reason is required.");

        var result = await Mediator.Send(new RequestEvidenceCommand(
            disputeId,
            adminId,
            request.Reason,
            request.Deadline,
            request.Target));

        return Ok(ApiResponse<AdminDisputeDetailResponse>.Ok(result, "Evidence requested successfully."));
    }

    [HttpPost("{disputeId:guid}/resolve")]
    public async Task<IActionResult> Resolve(
        Guid disputeId,
        [FromBody] AdminResolveDisputeRequest request)
    {
        if (!TryGetCurrentUserId(out var adminId))
            return InvalidTokenResponse();

        var command = new ResolveAdminDisputeCommand(
            disputeId,
            adminId,
            request.Resolution,
            request.ResolutionNote,
            request.InternalNotes,
            request.MilestoneAllocations?.Select(item => new AdminMilestoneAllocationInput(
                item.MilestoneId, item.Outcome, item.FreelancerAward, item.ClientRefund,
                item.PenaltyAmount, item.Reason)).ToList()
            ?? request.MilestoneDecisions?.Select(item => new AdminMilestoneAllocationInput(
                item.MilestoneId, item.Outcome, item.AdditionalReleaseToFreelancer,
                item.RefundToClient, 0m, null)).ToList() ?? [],
            (AdminContractAction)request.ContractAction,
            request.ClientViolation is null
                ? new AdminViolationInput(false, null, null, null)
                : new AdminViolationInput(request.ClientViolation.IsViolation,
                    request.ClientViolation.ViolationType, request.ClientViolation.Reason,
                    request.ClientViolation.Description),
            request.FreelancerViolation is null
                ? new AdminViolationInput(false, null, null, null)
                : new AdminViolationInput(request.FreelancerViolation.IsViolation,
                    request.FreelancerViolation.ViolationType, request.FreelancerViolation.Reason,
                    request.FreelancerViolation.Description));

        var result = await Mediator.Send(command);

        return Ok(ApiResponse<AdminDisputeDetailResponse>.Ok(result, "Dispute resolved successfully."));
    }
}
