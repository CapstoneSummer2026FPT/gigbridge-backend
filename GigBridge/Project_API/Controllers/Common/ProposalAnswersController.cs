using Application.Common.Models;
using Application.Features.Proposals.Common.Answers.GetProposalAnswers.Queries;
using Application.Features.Proposals.Common.DTOs;
using Application.Features.Proposals.Freelancer.Answers.CreateProposalAnswer.Commands;
using Application.Features.Proposals.Freelancer.Answers.CreateProposalAnswer.DTOs;
using Application.Features.Proposals.Freelancer.Answers.UpdateBulkProposalAnswers.Commands;
using Application.Features.Proposals.Freelancer.Answers.UpdateBulkProposalAnswers.DTOs;
using Application.Features.Proposals.Freelancer.Answers.UpdateProposalAnswer.Commands;
using Application.Features.Proposals.Freelancer.Answers.UpdateProposalAnswer.DTOs;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Common.Proposals;

[ApiController]
[Route("api/Proposals/{proposalId:guid}/answers")]
[Authorize]
public class ProposalAnswersController : BaseApiController
{
    [HttpGet]
    [Authorize(Roles = "Client,Freelancer")]
    public async Task<IActionResult> GetAnswers(Guid proposalId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var query = new GetProposalAnswersQuery(proposalId, userId, GetCurrentRole());
        var result = await Mediator.Send(query);

        return Ok(ApiResponse<IEnumerable<ProposalAnswerDto>>.Ok(result, "Success"));
    }

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Freelancer))]
    public async Task<IActionResult> CreateAnswer(
        Guid proposalId,
        [FromBody] CreateProposalAnswerRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var command = new CreateProposalAnswerCommand(proposalId, userId, request);
        var result = await Mediator.Send(command);

        return Ok(ApiResponse<ProposalAnswerDto>.Ok(result, "Answer created successfully"));
    }

    [HttpPatch("{answerId:guid}")]
    [Authorize(Roles = nameof(UserRole.Freelancer))]
    public async Task<IActionResult> UpdateAnswer(
        Guid proposalId,
        Guid answerId,
        [FromBody] UpdateProposalAnswerRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var command = new UpdateProposalAnswerCommand(proposalId, answerId, userId, request);
        var result = await Mediator.Send(command);

        return Ok(ApiResponse<ProposalAnswerDto>.Ok(result, "Answer updated successfully"));
    }

    [HttpPatch("bulk")]
    [Authorize(Roles = nameof(UserRole.Freelancer))]
    public async Task<IActionResult> UpdateAnswersBulk(
        Guid proposalId,
        [FromBody] UpdateBulkProposalAnswersRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var command = new UpdateBulkProposalAnswersCommand(proposalId, userId, request);
        var result = await Mediator.Send(command);

        return Ok(ApiResponse<IEnumerable<ProposalAnswerDto>>.Ok(result, "Answers updated successfully"));
    }

    private string GetCurrentRole()
    {
        if (User.IsInRole(nameof(UserRole.Client)))
        {
            return nameof(UserRole.Client);
        }

        if (User.IsInRole(nameof(UserRole.Freelancer)))
        {
            return nameof(UserRole.Freelancer);
        }

        return string.Empty;
    }
}
