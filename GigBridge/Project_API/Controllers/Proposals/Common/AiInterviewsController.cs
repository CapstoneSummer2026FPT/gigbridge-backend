using System.Diagnostics;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Common.Models;
using Application.Common.Models.Ai;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Project_API.Controllers.Common;

[ApiController]
[Route("api/ai-interviews")]
[Authorize(Roles = nameof(UserRole.Freelancer))]
public sealed class AiInterviewsController : BaseApiController
{
    private const long MaxAudioUploadBytes = 4 * 1024 * 1024; // 4mb
    private readonly IApplicationDbContext _context;
    private readonly IAiServiceClient _aiServiceClient;
    private readonly ILogger<AiInterviewsController> _logger;

    public AiInterviewsController(
        IApplicationDbContext context,
        IAiServiceClient aiServiceClient,
        ILogger<AiInterviewsController> logger)
    {
        _context = context;
        _aiServiceClient = aiServiceClient;
        _logger = logger;
    }

    [HttpPost("start")]
    public async Task<IActionResult> Start(
        [FromBody] StartAiInterviewRequest request,
        CancellationToken cancellationToken)
    {
        var total = Stopwatch.StartNew();
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var jobPost = await _context.Set<JobPost>()
            .AsNoTracking()
            .Include(item => item.JobPostSkills)
                .ThenInclude(item => item.Skills)
            .FirstOrDefaultAsync(item => item.JobPostsId == request.JobPostId, cancellationToken);

        if (jobPost is null)
        {
            return NotFound(ApiResponse<object>.NotFound("Job post not found"));
        }

        var skills = jobPost.JobPostSkills
            .Where(item => item.Skills is not null)
            .Select(item => item.Skills.Name)
            .Concat(jobPost.CustomSkillNames ?? [])
            .Where(skill => !string.IsNullOrWhiteSpace(skill))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var aiRequest = new AiInterviewStartRequestDto
        {
            JobId = jobPost.JobPostsId.ToString(),
            FreelancerId = userId.ToString(),
            JobTitle = jobPost.Title,
            JobDescription = jobPost.Description,
            JobSkills = skills,
            Mode = NormalizeMode(request.Mode),
            Language = NormalizeLanguage(request.Language)
        };

        var result = await _aiServiceClient.StartInterviewAsync(aiRequest, cancellationToken);
        total.Stop();

        _logger.LogInformation(
            "AI interview started in {ElapsedMs}ms for job {JobPostId}, freelancer {FreelancerId}, language {Language}, skills {SkillCount}",
            total.ElapsedMilliseconds,
            request.JobPostId,
            userId,
            aiRequest.Language,
            skills.Count);

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<AiInterviewQuestionResponseDto>.CreatedAt(
                result,
                "AI interview started successfully"));
    }

    [HttpPost("transcribe-audio")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxAudioUploadBytes)]
    public async Task<IActionResult> TranscribeAudio(
        [FromForm] TranscribeAiInterviewAudioRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out _))
        {
            return InvalidTokenResponse();
        }

        if (request.AudioFile is null || request.AudioFile.Length == 0)
        {
            return BadRequest(ApiResponse<object>.BadRequest("A recorded answer is required."));
        }

        await using var audioStream = request.AudioFile.OpenReadStream();
        var result = await _aiServiceClient.TranscribeInterviewAudioAsync(
            request.SessionId,
            audioStream,
            request.AudioFile.FileName,
            request.AudioFile.ContentType,
            NormalizeLanguage(request.Language),
            cancellationToken);

        return Ok(ApiResponse<AiInterviewDraftResponseDto>.Ok(
            result,
            "Answer transcribed successfully"));
    }

    [HttpPost("confirm-answer")]
    public async Task<IActionResult> ConfirmAnswer(
        [FromBody] ConfirmAiInterviewAnswerRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out _))
        {
            return InvalidTokenResponse();
        }

        var result = await _aiServiceClient.ConfirmInterviewAnswerAsync(
            new AiInterviewConfirmRequestDto
            {
                SessionId = request.SessionId,
                CorrectedText = request.CorrectedText
            },
            cancellationToken);

        return Ok(ApiResponse<AiInterviewQuestionResponseDto>.Ok(
            result,
            result.IsCompleted ? "Interview completed" : "Next question ready"));
    }

    [HttpGet("{sessionId}/questions/{questionIndex:int}/audio")]
    public async Task<IActionResult> GetQuestionAudio(
        string sessionId,
        int questionIndex,
        [FromHeader(Name = "X-Session-Token")] string audioAccessToken,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out _))
        {
            return InvalidTokenResponse();
        }

        if (string.IsNullOrWhiteSpace(audioAccessToken))
        {
            return BadRequest(ApiResponse<object>.BadRequest("The interview audio token is required."));
        }

        var result = await _aiServiceClient.GetInterviewQuestionAudioAsync(
            sessionId,
            questionIndex,
            audioAccessToken,
            cancellationToken);

        return Ok(ApiResponse<AiInterviewQuestionAudioResponseDto>.Ok(
            result,
            "Question audio status retrieved"));
    }

    [HttpGet("{sessionId}/questions/{questionIndex:int}/audio/stream")]
    public async Task<IActionResult> StreamQuestionAudio(
        string sessionId,
        int questionIndex,
        [FromHeader(Name = "X-Session-Token")] string audioAccessToken,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out _))
        {
            return InvalidTokenResponse();
        }

        if (string.IsNullOrWhiteSpace(audioAccessToken))
        {
            return BadRequest(ApiResponse<object>.BadRequest("The interview audio token is required."));
        }

        var result = await _aiServiceClient.StreamInterviewQuestionAudioAsync(
            sessionId,
            questionIndex,
            audioAccessToken,
            cancellationToken);
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
