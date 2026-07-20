using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using Application.Common.Interfaces.IService;
using Application.Common.Models;
using Application.Common.Models.Ai;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Chat;

[ApiController]
[Route("api/ai-assistant")]
[Authorize]
public class AiAssistantController : BaseApiController
{
    private readonly IAiServiceClient _aiServiceClient;

    public AiAssistantController(IAiServiceClient aiServiceClient)
    {
        _aiServiceClient = aiServiceClient;
    }

    [HttpPost("query")]
    public async Task<IActionResult> QueryChatBox(
        [FromBody] AiChatBoxRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out _))
        {
            return InvalidTokenResponse();
        }

        try
        {
            var result = await _aiServiceClient.QueryChatBoxAsync(request, cancellationToken);
            return Ok(ApiResponse<AiChatBoxResponseDto>.Ok(result, "AI response generated successfully."));
        }
        catch (HttpRequestException ex)
        {
            var statusCode = ex.StatusCode.HasValue ? (int)ex.StatusCode.Value : 500;
            return StatusCode(statusCode, ApiResponse<object>.Error(statusCode, ex.Message));
        }
        catch (System.Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.Error(500, ex.Message));
        }
    }
}
