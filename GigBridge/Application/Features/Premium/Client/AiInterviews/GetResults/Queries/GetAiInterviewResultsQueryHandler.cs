using System.Text.Json;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.InternalServices.Premium.Interfaces;
using Application.Features.Premium.Client.AiInterviews.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Premium.Client.AiInterviews.GetResults.Queries;

public sealed class GetAiInterviewResultsQueryHandler(
    IApplicationDbContext context,
    IPremiumAccessService premiumAccess)
    : IRequestHandler<GetAiInterviewResultsQuery, AiInterviewResultsDto>
{
    public async Task<AiInterviewResultsDto> Handle(
        GetAiInterviewResultsQuery query,
        CancellationToken cancellationToken)
    {
        await premiumAccess.RequirePremiumClientAsync(query.UserId, cancellationToken);
        var definition = await context.Set<AiInterviewDefinition>().AsNoTracking()
            .Include(x => x.Attempts).ThenInclude(x => x.Answers)
            .FirstOrDefaultAsync(x => x.AiInterviewDefinitionsId == query.InterviewId &&
                x.JobPostId == query.JobPostId && x.ClientUserId == query.UserId, cancellationToken)
            ?? throw new NotFoundException("AI interview not found.");
        var attempts = definition.Attempts.OrderByDescending(x => x.StartedAt).Select(x =>
            new AiInterviewAttemptResultDto(
                x.AiInterviewAttemptsId,
                x.Status.ToString(),
                x.OverallScore,
                x.CompatibilityScore,
                x.EvaluationSummary,
                ParseList(x.TechnicalSkillsJson),
                ParseList(x.SoftSkillsJson),
                x.RecommendedHire,
                x.StartedAt,
                x.CompletedAt,
                x.Answers.OrderBy(a => a.QuestionIndex).Select(a =>
                    new AiInterviewQuestionResultDto(a.QuestionIndex, a.QuestionText, a.Transcript, a.Score))
                    .ToList())).ToList();
        return new AiInterviewResultsDto(
            definition.AiInterviewDefinitionsId,
            definition.JobPostId,
            definition.Status.ToString(),
            attempts);
    }

    private static IReadOnlyList<string> ParseList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Array.Empty<string>();
        try { return JsonSerializer.Deserialize<List<string>>(value) ?? []; }
        catch (JsonException) { return Array.Empty<string>(); }
    }
}
