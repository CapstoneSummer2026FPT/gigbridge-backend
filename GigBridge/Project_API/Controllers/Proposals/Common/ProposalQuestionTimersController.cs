using Application.Common.Models;
using Application.Features.Proposals.Freelancer.QuestionTimers.Commands;
using Application.Common.InternalServices.Proposals.Models;
using Domain.Enums.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Common.Proposals;

[ApiController]
[Route("api/Proposals/{proposalId:guid}/question-timers/{questionId:guid}")]
[Authorize(Roles = nameof(UserRole.Freelancer))]
public class ProposalQuestionTimersController : BaseApiController
{
    [HttpPost("start")]
    public async Task<IActionResult> StartTimer(Guid proposalId, Guid questionId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var command = new StartProposalQuestionTimerCommand(proposalId, questionId, userId);
        var result = await Mediator.Send(command);

        return Ok(ApiResponse<QuestionTimerStateDto>.Ok(result, "Question timer started successfully"));
    }

    [HttpPost("complete")]
    public async Task<IActionResult> CompleteTimer(
        Guid proposalId,
        Guid questionId,
        [FromBody] CompleteQuestionTimerRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var command = new CompleteProposalQuestionTimerCommand(proposalId, questionId, userId, request);
        var result = await Mediator.Send(command);

        return Ok(ApiResponse<QuestionTimerStateDto>.Ok(result, "Question timer completed successfully"));
    }
}
