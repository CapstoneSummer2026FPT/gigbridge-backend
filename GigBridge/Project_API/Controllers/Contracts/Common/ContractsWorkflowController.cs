using Application.Common.Models;
using Application.Features.Contracts.Common.DTOs;
using Application.Features.Contracts.Completion.Client.Commands;
using Application.Features.Contracts.Completion.Client.DTOs;
using Application.Features.Contracts.Completion.Freelancer.Commands;
using Application.Features.Contracts.Completion.Freelancer.DTOs;
using Application.Features.Contracts.Details.Client.Submit.Commands;
using Application.Features.Contracts.Details.Client.Update.Commands;
using Application.Features.Contracts.Details.Client.Update.DTOs;
using Application.Features.Contracts.Details.Freelancer.Confirm.Commands;
using Application.Features.Contracts.Details.Freelancer.RequestChange.Commands;
using Application.Features.Contracts.Details.Freelancer.RequestChange.DTOs;
using Application.Features.Contracts.Escrow.Client.Fund.Commands;
using Application.Features.Contracts.Escrow.Client.Fund.DTOs;
using Application.Features.Contracts.Signing.Common.Sign.Commands;
using Application.Features.Contracts.Signing.Common.Sign.DTOs;
using Application.Features.Contracts.JobPostSetup.Complete.Commands;
using Application.Features.Contracts.MilestoneReview.Freelancer.Accept.Commands;
using Application.Features.Contracts.MilestoneReview.Freelancer.RequestChange.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Contracts.Common;

[ApiController]
[Route("api/contracts")]
[Authorize]
public sealed class ContractsWorkflowController : BaseApiController
{
    [HttpPut("{contractId}/details")]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> UpdateDetails(
        Guid contractId,
        [FromBody] UpdateContractDetailsRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new UpdateContractDetailsCommand(contractId, userId, request));

        return Ok(ApiResponse<ContractWorkflowResponse>.Ok(result, "Contract details updated"));
    }

    [HttpPost("{contractId}/details/submit")]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> SubmitDetails(Guid contractId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new SubmitContractDetailsCommand(contractId, userId));

        return Ok(ApiResponse<ContractWorkflowResponse>.Ok(result, "Contract details submitted"));
    }

    [HttpPost("{contractId}/details/confirm")]
    [Authorize(Roles = "Freelancer")]
    public async Task<IActionResult> ConfirmDetails(Guid contractId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new ConfirmContractDetailsCommand(contractId, userId));

        return Ok(ApiResponse<ContractWorkflowResponse>.Ok(result, "Contract details confirmed"));
    }

    [HttpPost("{contractId}/details/request-change")]
    [Authorize(Roles = "Freelancer")]
    public async Task<IActionResult> RequestChange(
        Guid contractId,
        [FromBody] RequestContractDetailsChangeRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new RequestContractDetailsChangeCommand(contractId, userId, request));

        return Ok(ApiResponse<ContractWorkflowResponse>.Ok(result, "Contract details change requested"));
    }

    [HttpPost("{contractId}/escrow/fund")]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> FundEscrow(Guid contractId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new FundContractEscrowCommand(contractId, userId));

        return Ok(ApiResponse<FundContractEscrowResponse>.Ok(result, "Escrow funded"));
    }

    [HttpPost("{contractId}/sign")]
    public async Task<IActionResult> Sign(
        Guid contractId,
        [FromBody] SignContractRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(
            new SignContractCommand(
                contractId,
                userId,
                request,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString()));

        return Ok(ApiResponse<ContractWorkflowResponse>.Ok(result, "Contract signed"));
    }

    [HttpPost("{contractId}/milestones/accept")]
    [Authorize(Roles = "Freelancer")]
    public async Task<IActionResult> AcceptMilestones(Guid contractId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new AcceptContractMilestonesCommand(contractId, userId));

        return Ok(ApiResponse<ContractWorkflowResponse>.Ok(result, "Milestones accepted"));
    }

    [HttpPost("{contractId}/milestones/request-change")]
    [Authorize(Roles = "Freelancer")]
    public async Task<IActionResult> RequestMilestoneChange(
        Guid contractId,
        [FromBody] RequestContractDetailsChangeRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new RequestContractMilestoneChangeCommand(contractId, userId, request));

        return Ok(ApiResponse<ContractWorkflowResponse>.Ok(result, "Milestone changes requested"));
    }

    [HttpPost("{contractId}/job-post-setup/complete")]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> CompleteJobPostSetup(Guid contractId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new CompleteJobPostContractSetupCommand(contractId, userId));

        return Ok(ApiResponse<bool>.Ok(result, "Job post setup completed and published"));
    }

    [HttpPost("{contractId}/end-project")]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> EndProject(Guid contractId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new EndProjectCommand(contractId, userId));

        return Ok(ApiResponse<EndProjectResponse>.Ok(result, "Project ended; final payout is ready to claim"));
    }

    [HttpPost("{contractId}/claim-final-payout")]
    [Authorize(Roles = "Freelancer")]
    public async Task<IActionResult> ClaimFinalPayout(Guid contractId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new ClaimFinalPayoutCommand(contractId, userId));

        return Ok(ApiResponse<ClaimFinalPayoutResponse>.Ok(result, "Final payout claimed"));
    }
}
