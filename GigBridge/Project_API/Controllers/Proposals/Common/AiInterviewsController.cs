using Application.Common.Models;
using Application.Common.Models.Ai;
using Application.Features.AiInterviews.Freelancer.Audio.Queries;
using Application.Features.AiInterviews.Freelancer.Confirm.Commands;
using Application.Features.AiInterviews.Freelancer.Start.Commands;
using Application.Features.AiInterviews.Freelancer.Transcribe.Commands;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Common;

[ApiController]
[Route("api/ai-interviews")]
[Authorize(Roles = nameof(UserRole.Freelancer))]
public sealed class AiInterviewsController : BaseApiController
{
    private const long MaxAudioUploadBytes = 4 * 1024 * 1024;

    [HttpPost("start")]
    public async Task<IActionResult> Start(
        [FromBody] StartAiInterviewRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var result = await Mediator.Send(new StartAiInterviewCommand(
            userId, request.JobPostId, request.InterviewDefinitionId,
            NormalizeMode(request.Mode), NormalizeLanguage(request.Language)), cancellationToken);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<AiInterviewQuestionResponseDto>.CreatedAt(result, "AI interview started successfully"));
    }

    [HttpPost("transcribe-audio")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxAudioUploadBytes)]
    public async Task<IActionResult> TranscribeAudio(
        [FromForm] TranscribeAiInterviewAudioRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        if (request.AudioFile is null || request.AudioFile.Length == 0)
            return BadRequest(ApiResponse<object>.BadRequest("A recorded answer is required."));
        await using var stream = request.AudioFile.OpenReadStream();
        var result = await Mediator.Send(new TranscribeAiInterviewCommand(
            userId, request.SessionId, stream, request.AudioFile.FileName,
            request.AudioFile.ContentType, NormalizeLanguage(request.Language)), cancellationToken);
        return Ok(ApiResponse<AiInterviewDraftResponseDto>.Ok(result, "Answer transcribed successfully"));
    }

    [HttpPost("confirm-answer")]
    public async Task<IActionResult> ConfirmAnswer(
        [FromBody] ConfirmAiInterviewAnswerRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var result = await Mediator.Send(new ConfirmAiInterviewAnswerCommand(
            userId, request.SessionId, request.CorrectedText), cancellationToken);
        return Ok(ApiResponse<AiInterviewQuestionResponseDto>.Ok(
            result, result.IsCompleted ? "Interview completed" : "Next question ready"));
    }

    [HttpGet("{sessionId}/questions/{questionIndex:int}/audio")]
    public async Task<IActionResult> GetQuestionAudio(
        string sessionId,
        int questionIndex,
        [FromHeader(Name = "X-Session-Token")] string audioAccessToken,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out _)) return InvalidTokenResponse();
        if (string.IsNullOrWhiteSpace(audioAccessToken))
            return BadRequest(ApiResponse<object>.BadRequest("The interview audio token is required."));
        var result = await Mediator.Send(new GetAiInterviewQuestionAudioQuery(
            sessionId, questionIndex, audioAccessToken), cancellationToken);
        return Ok(ApiResponse<AiInterviewQuestionAudioResponseDto>.Ok(
            result, "Question audio status retrieved"));
    }

    [HttpGet("{sessionId}/questions/{questionIndex:int}/audio/stream")]
    public async Task<IActionResult> StreamQuestionAudio(
        string sessionId,
        int questionIndex,
        [FromHeader(Name = "X-Session-Token")] string audioAccessToken,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out _)) return InvalidTokenResponse();
        if (string.IsNullOrWhiteSpace(audioAccessToken))
            return BadRequest(ApiResponse<object>.BadRequest("The interview audio token is required."));
        var result = await Mediator.Send(new StreamAiInterviewQuestionAudioQuery(
            sessionId, questionIndex, audioAccessToken), cancellationToken);
        Response.Headers.CacheControl = "no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(result.AudioStream, result.ContentType);
    }

    private static string NormalizeMode(string? mode) =>
        string.Equals(mode, "text", StringComparison.OrdinalIgnoreCase) ? "text" : "voice";

    private static string NormalizeLanguage(string? language)
    {
        if (string.Equals(language, "vi", StringComparison.OrdinalIgnoreCase)) return "vi";
        if (string.Equals(language, "en", StringComparison.OrdinalIgnoreCase)) return "en";
        return "auto";
    }
}

public sealed class StartAiInterviewRequest
{
    public Guid JobPostId { get; set; }
    public Guid? InterviewDefinitionId { get; set; }
    public string Mode { get; set; } = "voice";
    public string Language { get; set; } = "auto";
}

public sealed class TranscribeAiInterviewAudioRequest
{
    public string SessionId { get; set; } = string.Empty;
    public string Language { get; set; } = "auto";
    public IFormFile AudioFile { get; set; } = null!;
}

public sealed class ConfirmAiInterviewAnswerRequest
{
    public string SessionId { get; set; } = string.Empty;
    public string? CorrectedText { get; set; }
}
