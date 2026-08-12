using Application.Common.Models;
using Application.Features.JobInvitations.Client.BulkCreateInvitations.Commands;
using Application.Features.JobInvitations.Client.CancelInvitation.Commands;
using Application.Features.JobInvitations.Client.CreateInvitation.Commands;
using Application.Features.JobInvitations.Client.GetInvitationsForJob.Queries;
using Application.Features.JobInvitations.Client.GetMySentInvitations.Queries;
using Application.Features.JobInvitations.Common.DTOs;
using Application.Features.JobInvitations.Freelancer.ApplyInvitation.Commands;
using Application.Features.JobInvitations.Freelancer.DeclineInvitation.Commands;
using Application.Features.JobInvitations.Freelancer.GetMyInvitations.Queries;
using Application.Features.JobInvitations.Freelancer.ViewInvitation.Commands;
using Domain.Enums.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Jobs.Common;

[ApiController]
[Route("api/JobInvitations")]
[Authorize]
public sealed class JobInvitationsController : BaseApiController
{
    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Client))]
    public async Task<IActionResult> CreateInvitation([FromBody] CreateJobInvitationRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new CreateJobInvitationCommand(userId, request));
        return Ok(ApiResponse<JobInvitationDto>.Ok(result, "Job invitation sent successfully"));
    }

    [HttpPost("bulk")]
    [Authorize(Roles = nameof(UserRole.Client))]
    public async Task<IActionResult> BulkCreateInvitations([FromBody] BulkCreateJobInvitationsRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new BulkCreateJobInvitationsCommand(userId, request));
        return Ok(ApiResponse<BulkJobInvitationResultDto>.Ok(result, "Job invitations processed successfully"));
    }

    [HttpGet("my-sent")]
    [Authorize(Roles = nameof(UserRole.Client))]
    public async Task<IActionResult> GetMySentInvitations(
        [FromQuery] int? status,
        [FromQuery] Guid? jobPostId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GetMySentJobInvitationsQuery(userId, status, jobPostId, page, pageSize));
        return Ok(ApiResponse<IEnumerable<JobInvitationDto>>.Ok(result, "Success"));
    }

    [HttpGet("job/{jobPostId}")]
    [Authorize(Roles = nameof(UserRole.Client))]
    public async Task<IActionResult> GetInvitationsForJob(Guid jobPostId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GetJobInvitationsForJobQuery(userId, jobPostId));
        return Ok(ApiResponse<IEnumerable<JobInvitationDto>>.Ok(result, "Success"));
    }

    [HttpPatch("{invitationId}/cancel")]
    [Authorize(Roles = nameof(UserRole.Client))]
    public async Task<IActionResult> CancelInvitation(Guid invitationId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new CancelJobInvitationCommand(userId, invitationId));
        return Ok(ApiResponse<JobInvitationDto>.Ok(result, "Job invitation cancelled successfully"));
    }

    [HttpGet("my-invitations")]
    [Authorize(Roles = nameof(UserRole.Freelancer))]
    public async Task<IActionResult> GetMyInvitations(
        [FromQuery] int? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GetMyJobInvitationsQuery(userId, status, page, pageSize));
        return Ok(ApiResponse<IEnumerable<JobInvitationDto>>.Ok(result, "Success"));
    }

    [HttpPatch("{invitationId}/view")]
    [Authorize(Roles = nameof(UserRole.Freelancer))]
    public async Task<IActionResult> ViewInvitation(Guid invitationId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new ViewJobInvitationCommand(userId, invitationId));
        return Ok(ApiResponse<JobInvitationDto>.Ok(result, "Job invitation marked as viewed"));
    }

    [HttpPatch("{invitationId}/apply")]
    [Authorize(Roles = nameof(UserRole.Freelancer))]
    public async Task<IActionResult> ApplyInvitation(Guid invitationId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new ApplyJobInvitationCommand(userId, invitationId));
        return Ok(ApiResponse<JobInvitationDto>.Ok(result, "Job invitation marked as applied"));
    }

    [HttpPatch("{invitationId}/decline")]
    [Authorize(Roles = nameof(UserRole.Freelancer))]
    public async Task<IActionResult> DeclineInvitation(
        Guid invitationId,
        [FromBody] DeclineJobInvitationRequest? request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new DeclineJobInvitationCommand(userId, invitationId, request?.Reason));
        return Ok(ApiResponse<JobInvitationDto>.Ok(result, "Job invitation declined successfully"));
    }
}
