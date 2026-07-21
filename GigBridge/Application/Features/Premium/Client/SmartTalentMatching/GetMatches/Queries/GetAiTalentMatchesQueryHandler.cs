using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Common.Models.Ai;
using Application.Features.Premium.Client.SmartTalentMatching.GetMatches.DTOs;
using MediatR;

namespace Application.Features.Premium.Client.SmartTalentMatching.GetMatches.Queries;

public sealed class GetAiTalentMatchesQueryHandler(
    IApplicationDbContext context, IPremiumAccessService premiumAccess,
    IDateTimeService clock, IAiServiceClient aiServiceClient)
    : IRequestHandler<GetAiTalentMatchesQuery, TalentMatchingResultDto>
{
    public async Task<TalentMatchingResultDto> Handle(
        GetAiTalentMatchesQuery query, CancellationToken cancellationToken)
    {
        await premiumAccess.RequirePremiumClientAsync(query.UserId, cancellationToken);
        var shortlistSize = Math.Min(Math.Max(query.TopK * 3, 10), 20);
        var shortlist = await TalentMatchingCandidateLoader.LoadAsync(
            context, query.UserId, query.JobPostId, clock.UtcNow, shortlistSize, query.Filters,
            cancellationToken);
        if (shortlist.Candidates.Count == 0)
            return new TalentMatchingResultDto(query.JobPostId, "ai", false, []);

        TalentRerankResponseDto aiResult;
        try
        {
            aiResult = await aiServiceClient.RerankTalentAsync(BuildRequest(shortlist, query.TopK), cancellationToken);
        }
        catch (Exception exception) when (exception is ExternalServiceException or HttpRequestException or TaskCanceledException)
        {
            return DeterministicFallback(shortlist, query.JobPostId, query.TopK);
        }

        var known = shortlist.Candidates.ToDictionary(item => item.Scored.Match.FreelancerId);
        var semanticByFreelancer = new Dictionary<Guid, TalentRerankMatchDto>();
        foreach (var aiMatch in aiResult.Matches)
        {
            if (!Guid.TryParse(aiMatch.FreelancerId, out var freelancerId) ||
                !known.ContainsKey(freelancerId) || semanticByFreelancer.ContainsKey(freelancerId) ||
                aiMatch.SemanticScore is < 0d or > 1d) continue;
            semanticByFreelancer[freelancerId] = aiMatch;
        }

        var matches = shortlist.Candidates.Select(ranked =>
        {
            var deterministic = ranked.Scored.Match;
            semanticByFreelancer.TryGetValue(deterministic.FreelancerId, out var aiMatch);
            var semanticPoints = Math.Round((decimal)(aiMatch?.SemanticScore ?? 0.5d) * 50m, 2);
            return deterministic with
            {
                MatchPercentage = Math.Round(Math.Min(deterministic.MatchPercentage + semanticPoints, 100m), 2),
                Reasons = (aiMatch?.MatchReasons ?? []).Concat(deterministic.Reasons)
                        .Where(reason => !string.IsNullOrWhiteSpace(reason) && reason.Length <= 300)
                        .Select(reason => reason.Trim()).Take(5)
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                ScoreBreakdown = deterministic.ScoreBreakdown with { AiSemantic = semanticPoints }
            };
        }).ToList();
        var final = matches.OrderByDescending(match => match.MatchPercentage)
            .ThenBy(match => match.FreelancerId).Take(query.TopK).ToList();
        return new TalentMatchingResultDto(query.JobPostId, "ai", false, final);
    }

    private static TalentRerankRequestDto BuildRequest(TalentMatchingShortlist shortlist, int topK) => new()
    {
        TopK = topK,
        Job = new TalentRerankJobDto
        {
            JobId = shortlist.Job.JobPostId.ToString(), Title = shortlist.JobTitle,
            Description = shortlist.JobDescription, Industry = null,
            Skills = shortlist.Job.Skills.Select(skill => skill.Name)
                .Concat(shortlist.Job.CustomSkills).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        },
        Candidates = shortlist.Candidates.Select(item => new TalentRerankCandidateDto
        {
            FreelancerId = item.Scored.Match.FreelancerId.ToString(), Title = item.Candidate.Title,
            Bio = Truncate(item.Candidate.Bio, 600),
            Skills = item.Candidate.Skills.Select(skill => skill.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            WorkHistory = item.Candidate.WorkExperiences.Take(5)
                .Select(experience => $"{experience.Title} at {experience.CompanyName}: {Truncate(experience.Description, 250)}")
                .ToList(),
            DeterministicScore = item.Scored.Match.MatchPercentage
        }).ToList()
    };

    private static string? Truncate(string? value, int maximumLength) =>
        string.IsNullOrWhiteSpace(value) || value.Length <= maximumLength ? value : value[..maximumLength];

    private static TalentMatchingResultDto DeterministicFallback(
        TalentMatchingShortlist shortlist, Guid jobPostId, int topK) => new(jobPostId,
        "deterministic-fallback", false, shortlist.Candidates.Take(topK)
            .Select(item => item.Scored.Match).ToList());
}
