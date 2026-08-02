using System.Diagnostics;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Common.Models.Ai;
using Application.Features.Premium.Client.SmartTalentMatching.GetMatches.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Application.Features.Premium.Client.SmartTalentMatching.GetMatches.Queries;

public sealed class GetAiTalentMatchesQueryHandler(
    IApplicationDbContext context,
    IPremiumAccessService premiumAccess,
    IDateTimeService clock,
    IAiServiceClient aiServiceClient,
    IConfiguration configuration,
    ILogger<GetAiTalentMatchesQueryHandler> logger)
    : IRequestHandler<GetAiTalentMatchesQuery, AiTalentMatchingResultDto>
{
    private const string Mode = "ai-algorithm-v2";
    private const string AlgorithmVersion = "2.0";
    private const string ScoringVersion = "weighted-features-v1";

    public async Task<AiTalentMatchingResultDto> Handle(
        GetAiTalentMatchesQuery query,
        CancellationToken cancellationToken)
    {
        var featureEnabled = bool.TryParse(
            configuration["FeatureFlags:AiSmartTalentMatchingV1"],
            out var enabled) && enabled;
        if (!featureEnabled)
        {
            throw new ForbiddenAccessException("AI smart talent matching is not enabled.");
        }

        await premiumAccess.RequirePremiumClientAsync(query.UserId, cancellationToken);
        var stopwatch = Stopwatch.StartNew();
        var candidateFilters = query.Filters is null
            ? null
            : new TalentMatchingFiltersDto(
                Availability: query.Filters.Availability,
                MajorCategoryId: query.Filters.MajorCategoryId,
                SkillIds: query.Filters.SkillIds);
        var pool = await TalentMatchingCandidateLoader.LoadAsync(
            context, query.UserId, query.JobPostId, candidateFilters, cancellationToken);
        var now = clock.UtcNow;
        var run = new TalentMatchRun
        {
            TalentMatchRunId = Guid.NewGuid(),
            ClientUserId = query.UserId,
            JobPostId = query.JobPostId,
            AlgorithmVersion = AlgorithmVersion,
            ScoringVersion = ScoringVersion,
            EligibleCandidateCount = pool.Candidates.Count,
            Status = (int)TalentMatchRunStatus.Running,
            CreatedAt = now
        };
        context.Set<TalentMatchRun>().Add(run);

        if (pool.Candidates.Count == 0)
        {
            stopwatch.Stop();
            run.Status = (int)TalentMatchRunStatus.NoCandidates;
            run.CompletedAt = clock.UtcNow;
            run.LatencyMilliseconds = stopwatch.ElapsedMilliseconds;
            context.Set<PremiumUsageEvent>().Add(new PremiumUsageEvent
            {
                PremiumUsageEventId = Guid.NewGuid(), Type = PremiumUsageEventType.TalentMatching,
                UserId = query.UserId, JobPostId = query.JobPostId,
                IdempotencyKey = $"talent-match:{run.TalentMatchRunId:N}", OccurredAt = run.CompletedAt.Value,
                Metadata = "{\"candidateCount\":0}"
            });
            await context.SaveChangesAsync(cancellationToken);
            return new AiTalentMatchingResultDto(run.TalentMatchRunId, query.JobPostId,
                Mode, AlgorithmVersion, []);
        }

        TalentRerankResponseDto aiResult;
        try
        {
            var aiRerankCount = Math.Min(30, pool.Candidates.Count);
            aiResult = await RequestValidAiResultAsync(pool, aiRerankCount, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is ExternalServiceException or HttpRequestException or TaskCanceledException)
        {
            stopwatch.Stop();
            run.Status = (int)TalentMatchRunStatus.Failed;
            run.CompletedAt = clock.UtcNow;
            run.LatencyMilliseconds = stopwatch.ElapsedMilliseconds;
            run.FailureCode = "AI_MATCHING_UNAVAILABLE";
            await context.SaveChangesAsync(CancellationToken.None);
            throw new ExternalServiceException(
                "AI_MATCHING_UNAVAILABLE: Smart matching is temporarily unavailable. Please retry.", exception);
        }

        var candidatesById = pool.Candidates.ToDictionary(candidate => candidate.FreelancerProfileId);
        var evaluated = aiResult.Matches.Select(aiMatch =>
        {
            var freelancerId = Guid.Parse(aiMatch.FreelancerId);
            var candidate = candidatesById[freelancerId];
            var evidence = AiTalentEvidenceScorer.Score(pool.Job, candidate);
            var embedding = Round((decimal)aiMatch.EmbeddingScore);
            var algorithm = Round((decimal)aiMatch.AlgorithmScore);
            var finalScore = Round(Math.Clamp(
                0.45m * embedding + 0.35m * algorithm + 0.20m * evidence.Score,
                0m, 100m));
            var agreement = Math.Clamp(100m - Math.Abs(embedding - algorithm), 0m, 100m);
            var confidenceScore = 0.6m * evidence.DataCoverage + 0.4m * agreement;
            var confidence = confidenceScore >= 75m ? "high" : confidenceScore >= 50m ? "medium" : "low";
            var semanticStrengths = Clean(aiMatch.SemanticStrengths, 5, 200);
            var reasons = evidence.Reasons.Concat(Clean(aiMatch.MatchReasons, 3, 300))
                .Distinct(StringComparer.OrdinalIgnoreCase).Take(6).ToList();

            return new PendingMatch(
                candidate,
                finalScore,
                confidence,
                embedding,
                algorithm,
                evidence,
                Round(agreement),
                Round(confidenceScore),
                semanticStrengths,
                reasons);
        })
        .OrderByDescending(match => match.FinalScore)
        .ThenBy(match => match.Candidate.FreelancerProfileId)
        .Take(query.TopK)
        .ToList();

        var matches = evaluated.Select((match, index) => new AiTalentMatchDto(
            match.Candidate.FreelancerProfileId,
            match.Candidate.UserId,
            match.Candidate.DisplayName,
            match.Candidate.Title,
            match.Candidate.AvatarUrl,
            match.Candidate.Location,
            match.Candidate.Availability,
            index + 1,
            match.FinalScore,
            match.Confidence,
            new AiTalentMatchConfidenceBreakdownDto(
                match.Evidence.DataCoverage,
                match.ScoreAgreement,
                match.ConfidenceScore),
            new AiTalentMatchScoreBreakdownDto(match.Embedding, match.Algorithm, match.Evidence.Score),
            match.Evidence.MatchedSkills,
            match.Evidence.MissingSkills,
            match.SemanticStrengths,
            match.Reasons,
            match.Candidate.AverageRating,
            match.Candidate.ReviewCount,
            match.Candidate.CompletedContractCount,
            match.Candidate.EloPoints)).ToList();

        if (bool.TryParse(
                configuration["FeatureFlags:AiSmartTalentMatchingShadowComparison"],
                out var shadowEnabled) && shadowEnabled)
        {
            var shadowIds = RankWithLegacyScorer(pool, query.TopK);
            var aiIds = matches.Select(match => match.FreelancerProfileId).ToHashSet();
            logger.LogInformation(
                "AI talent matching shadow comparison {MatchRunId} for job {JobPostId}: AI={AiCount}, Legacy={LegacyCount}, OverlapAtK={OverlapAtK}",
                run.TalentMatchRunId,
                query.JobPostId,
                aiIds.Count,
                shadowIds.Count,
                shadowIds.Count(aiIds.Contains));
        }

        foreach (var match in matches)
        {
            context.Set<TalentMatchResult>().Add(new TalentMatchResult
            {
                TalentMatchResultId = Guid.NewGuid(),
                TalentMatchRunId = run.TalentMatchRunId,
                FreelancerProfileId = match.FreelancerProfileId,
                Rank = match.Rank,
                EmbeddingScore = match.ScoreBreakdown.Embedding,
                AlgorithmScore = match.ScoreBreakdown.Algorithm,
                EvidenceScore = match.ScoreBreakdown.Evidence,
                FinalScore = match.FinalScore,
                Confidence = match.Confidence,
                MatchedSkills = match.MatchedSkills.ToArray(),
                MissingSkills = match.MissingSkills.ToArray(),
                SemanticStrengths = match.SemanticStrengths.ToArray(),
                Reasons = match.Reasons.ToArray(),
                CreatedAt = clock.UtcNow
            });
        }

        stopwatch.Stop();
        run.AlgorithmVersion = string.IsNullOrWhiteSpace(aiResult.AlgorithmVersion)
            ? AlgorithmVersion
            : aiResult.AlgorithmVersion;
        run.EmbeddingModel = NullIfWhiteSpace(aiResult.EmbeddingModel);
        run.ScoringVersion = string.IsNullOrWhiteSpace(aiResult.ScoringVersion)
            ? ScoringVersion
            : aiResult.ScoringVersion;
        run.ReturnedCandidateCount = matches.Count;
        run.LatencyMilliseconds = stopwatch.ElapsedMilliseconds;
        run.Status = (int)TalentMatchRunStatus.Succeeded;
        run.CompletedAt = clock.UtcNow;
        context.Set<PremiumUsageEvent>().Add(new PremiumUsageEvent
        {
            PremiumUsageEventId = Guid.NewGuid(), Type = PremiumUsageEventType.TalentMatching,
            UserId = query.UserId, JobPostId = query.JobPostId,
            IdempotencyKey = $"talent-match:{run.TalentMatchRunId:N}", OccurredAt = run.CompletedAt.Value,
            Metadata = System.Text.Json.JsonSerializer.Serialize(new { candidateCount = matches.Count })
        });
        await context.SaveChangesAsync(cancellationToken);

        return new AiTalentMatchingResultDto(run.TalentMatchRunId, query.JobPostId,
            Mode, run.AlgorithmVersion, matches);
    }

    private async Task<TalentRerankResponseDto> RequestValidAiResultAsync(
        AiTalentMatchingPool pool,
        int topK,
        CancellationToken cancellationToken)
    {
        Exception? lastException = null;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                var response = await aiServiceClient.RerankTalentAsync(
                    BuildRequest(pool, topK), cancellationToken);
                ValidateAiResponse(response, pool, topK);
                return response;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is ExternalServiceException or HttpRequestException or TaskCanceledException)
            {
                lastException = exception;
                if (attempt == 0)
                {
                    await Task.Delay(150, cancellationToken);
                }
            }
        }

        throw new ExternalServiceException(
            "AI matching failed after retry.", lastException ?? new InvalidOperationException());
    }

    private static void ValidateAiResponse(
        TalentRerankResponseDto response,
        AiTalentMatchingPool pool,
        int topK)
    {
        var expected = Math.Min(topK, pool.Candidates.Count);
        if (response.Matches.Count != expected)
        {
            throw new ExternalServiceException("AI matching returned an incomplete candidate set.");
        }

        var knownIds = pool.Candidates.Select(candidate => candidate.FreelancerProfileId).ToHashSet();
        var returnedIds = new HashSet<Guid>();
        foreach (var match in response.Matches)
        {
            if (!Guid.TryParse(match.FreelancerId, out var freelancerId) ||
                !knownIds.Contains(freelancerId) ||
                !returnedIds.Add(freelancerId) ||
                match.EmbeddingScore is < 0d or > 100d ||
                match.AlgorithmScore is < 0d or > 100d)
            {
                throw new ExternalServiceException("AI matching returned an invalid candidate result.");
            }
        }
    }

    private static TalentRerankRequestDto BuildRequest(AiTalentMatchingPool pool, int topK) => new()
    {
        TopK = topK,
        AlgorithmVersion = AlgorithmVersion,
        ScoringVersion = ScoringVersion,
        Job = new TalentRerankJobDto
        {
            JobId = pool.Job.JobPostId.ToString(),
            Title = pool.Job.Title,
            Description = Truncate(pool.Job.Description, 4000) ?? string.Empty,
            Industry = pool.Job.ClientIndustry,
            MajorId = pool.Job.MajorId?.ToString(),
            MajorName = pool.Job.MajorName,
            MajorCategoryId = pool.Job.MajorCategoryId?.ToString(),
            CategoryName = pool.Job.CategoryName,
            Skills = pool.Job.Skills.Select(skill => skill.Name).ToList(),
            CustomSkills = pool.Job.CustomSkills.ToList(),
            Location = pool.Job.Location,
            EstimatedDuration = pool.Job.EstimatedDuration
        },
        Candidates = pool.Candidates.Select(candidate => new TalentRerankCandidateDto
        {
            FreelancerId = candidate.FreelancerProfileId.ToString(),
            Title = Truncate(candidate.Title, 300),
            Bio = Truncate(candidate.Bio, 1200),
            Location = Truncate(candidate.Location, 300),
            Availability = candidate.Availability,
            MajorId = candidate.MajorId?.ToString(),
            MajorName = candidate.MajorName,
            Categories = candidate.CategoryNames.ToList(),
            Skills = candidate.Skills.Select(skill => skill.Name).ToList(),
            VerifiedWork = candidate.VerifiedWork.Select(work => new TalentRerankVerifiedWorkDto
            {
                ContractId = work.ContractId.ToString(),
                Title = Truncate(work.Title, 300) ?? string.Empty,
                Description = Truncate(work.Description, 500),
                MajorName = work.MajorName,
                CategoryName = work.CategoryName,
                Skills = work.Skills.Select(skill => skill.Name).ToList()
            }).ToList()
        }).ToList()
    };

    private static IReadOnlyList<string> Clean(IEnumerable<string>? values, int limit, int maxLength) =>
        (values ?? []).Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value.Trim())
        .Where(value => value.Length <= maxLength)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Take(limit)
        .ToList();

    private static string? Truncate(string? value, int maximumLength) =>
        string.IsNullOrWhiteSpace(value) || value.Length <= maximumLength ? value : value[..maximumLength];

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static decimal Round(decimal value) => Math.Round(value, 2);

    private static IReadOnlyList<Guid> RankWithLegacyScorer(
        AiTalentMatchingPool pool,
        int topK)
    {
        var legacyJob = new TalentScoringJob(
            pool.Job.JobPostId,
            pool.Job.MajorCategoryId,
            pool.Job.MajorId,
            pool.Job.Skills.Select(skill =>
                new TalentScoringSkill(skill.SkillId, skill.Name, IsRequired: false)).ToList(),
            pool.Job.CustomSkills);

        return pool.Candidates.Select(candidate =>
            {
                var legacyCandidate = new TalentScoringCandidate(
                    candidate.FreelancerProfileId,
                    candidate.UserId,
                    candidate.DisplayName,
                    candidate.Title,
                    candidate.Bio,
                    candidate.Availability,
                    ProfileCompletionScore: 0,
                    candidate.EloPoints,
                    candidate.AverageRating,
                    candidate.ReviewCount,
                    candidate.CompletedContractCount,
                    candidate.MajorId,
                    candidate.MajorCategoryIds,
                    candidate.Skills.Select(skill =>
                        new TalentScoringSkill(skill.SkillId, skill.Name, IsRequired: false)).ToList(),
                    [],
                    candidate.VerifiedWork.Select(work => new TalentVerifiedContractEvidence(
                        work.ContractId,
                        work.MajorCategoryId,
                        work.MajorId,
                        work.Skills.Select(skill => skill.SkillId).ToHashSet())).ToList());
                return TalentMatchScorer.Score(legacyJob, legacyCandidate);
            })
            .Where(result => result is not null)
            .OrderByDescending(result => result!.Match.MatchPercentage)
            .ThenBy(result => result!.Match.FreelancerId)
            .Take(topK)
            .Select(result => result!.Match.FreelancerId)
            .ToList();
    }

    private sealed record PendingMatch(
        AiTalentMatchingCandidate Candidate,
        decimal FinalScore,
        string Confidence,
        decimal Embedding,
        decimal Algorithm,
        AiTalentEvidenceScore Evidence,
        decimal ScoreAgreement,
        decimal ConfidenceScore,
        IReadOnlyList<string> SemanticStrengths,
        IReadOnlyList<string> Reasons);
}
