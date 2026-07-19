using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Common.Models;
using Application.Common.Models.Ai;
using Application.Features.AiInterviews.Freelancer.Audio.Queries;
using Application.Features.AiInterviews.Freelancer.Confirm.Commands;
using Application.Features.AiInterviews.Freelancer.Start.Commands;
using Application.Features.AiInterviews.Freelancer.Transcribe.Commands;
using Application.Features.AiInterviews.Freelancer.Requirement;
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
    private readonly IDateTimeService _dateTimeService;

    public AiInterviewsController(
        IApplicationDbContext context,
        IAiServiceClient aiServiceClient,
        ILogger<AiInterviewsController> logger,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _aiServiceClient = aiServiceClient;
        _logger = logger;
        _dateTimeService = dateTimeService;
    }

    [HttpPost("start")]
    public async Task<IActionResult> Start(
        [FromBody] StartAiInterviewRequest request,
        CancellationToken cancellationToken)
    {
        var total = System.Diagnostics.Stopwatch.StartNew();
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var jobPost = await _context.Set<Domain.Entities.JobPost>()
            .AsNoTracking()
            .Include(item => item.JobPostSkills)
                .ThenInclude(item => item.Skills)
            .Include(item => item.JobPostQuestions)
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

        var jobQuestions = jobPost.JobPostQuestions
            .OrderBy(q => q.OrderIndex)
            .Select(q => q.QuestionText)
            .ToList();

        var aiRequest = new AiInterviewStartRequestDto
        {
            JobId = jobPost.JobPostsId.ToString(),
            FreelancerId = userId.ToString(),
            JobTitle = jobPost.Title,
            JobDescription = jobPost.Description,
            JobSkills = skills,
            JobQuestions = jobQuestions,
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

        // Map answer back to database if there's an associated proposal
        if (!string.IsNullOrEmpty(result.JobId) && !string.IsNullOrEmpty(result.FreelancerId))
        {
            if (Guid.TryParse(result.JobId, out var jobId) && Guid.TryParse(result.FreelancerId, out var freelancerUserId))
            {
                var proposal = await _context.Set<Domain.Entities.Proposal>()
                    .Include(p => p.FreelancerProfiles)
                    .FirstOrDefaultAsync(p => p.JobPostsId == jobId && p.FreelancerProfiles.UserId == freelancerUserId, cancellationToken);

                if (proposal is not null)
                {
                    int answeredIndex = result.IsCompleted ? result.QuestionIndex : result.QuestionIndex - 1;

                    var questions = await _context.Set<Domain.Entities.JobPostQuestion>()
                        .Where(q => q.JobPostsId == jobId)
                        .OrderBy(q => q.OrderIndex)
                        .ToListAsync(cancellationToken);

                    if (answeredIndex >= 1 && answeredIndex <= questions.Count)
                    {
                        var question = questions[answeredIndex - 1];
                        var now = _dateTimeService.UtcNow;

                        // 1. Upsert ProposalAnswer
                        var existingAnswer = await _context.Set<Domain.Entities.ProposalAnswer>()
                            .FirstOrDefaultAsync(a => a.ProposalsId == proposal.ProposalsId && a.JobPostQuestionsId == question.JobPostQuestionsId, cancellationToken);

                        if (existingAnswer is null)
                        {
                            _context.Set<Domain.Entities.ProposalAnswer>().Add(new Domain.Entities.ProposalAnswer
                            {
                                ProposalAnswersId = Guid.NewGuid(),
                                ProposalsId = proposal.ProposalsId,
                                JobPostQuestionsId = question.JobPostQuestionsId,
                                AnswerText = request.CorrectedText ?? string.Empty,
                                CreatedAt = now
                            });
                        }
                        else
                        {
                            existingAnswer.AnswerText = request.CorrectedText ?? string.Empty;
                            existingAnswer.UpdatedAt = now;
                        }

                        // 2. Lock ProposalQuestionTimer
                        var existingTimer = await _context.Set<Domain.Entities.ProposalQuestionTimer>()
                            .FirstOrDefaultAsync(t => t.ProposalsId == proposal.ProposalsId && t.JobPostQuestionsId == question.JobPostQuestionsId, cancellationToken);

                        if (existingTimer is null)
                        {
                            _context.Set<Domain.Entities.ProposalQuestionTimer>().Add(new Domain.Entities.ProposalQuestionTimer
                            {
                                ProposalQuestionTimersId = Guid.NewGuid(),
                                ProposalsId = proposal.ProposalsId,
                                JobPostQuestionsId = question.JobPostQuestionsId,
                                FreelancerUserId = freelancerUserId,
                                StartedAt = now,
                                ExpiresAt = now,
                                IsLocked = true,
                                LockedReason = (int)QuestionTimerLockedReason.Completed,
                                CompletedAt = now,
                                CreatedAt = now
                            });
                        }
                        else if (!existingTimer.IsLocked)
                        {
                            existingTimer.IsLocked = true;
                            existingTimer.LockedReason = (int)QuestionTimerLockedReason.Completed;
                            existingTimer.CompletedAt = now;
                            existingTimer.UpdatedAt = now;
                        }

                        await _context.SaveChangesAsync(cancellationToken);
                    }
                }
            }
        }
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

    [HttpGet("requirement/{jobPostId:guid}")]
    public async Task<IActionResult> Requirement(Guid jobPostId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var result = await Mediator.Send(
            new GetAiInterviewRequirementQuery(userId, jobPostId), cancellationToken);
        return Ok(ApiResponse<AiInterviewRequirementDto>.Ok(result, "Success"));
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
