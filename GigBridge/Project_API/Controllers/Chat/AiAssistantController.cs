using System.Threading;
using System.Threading.Tasks;
using Application.Common.Models;
using Application.Common.Models.Ai;
using Application.Features.AiAssistant.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Chat;

[ApiController]
[Route("api/ai-assistant")]
[Authorize]
public class AiAssistantController : BaseApiController
{
    [HttpPost("query")]
    public async Task<IActionResult> QueryChatBox(
        [FromBody] AiChatBoxRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out _))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GetAiChatBoxQuery(request), cancellationToken);
        return Ok(ApiResponse<AiChatBoxResponseDto>.Ok(result, "AI response generated successfully."));
    }
}
