using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Common.Models.Ai;
using Application.Features.Premium.Client.SmartTalentMatching.GetMatches.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Premium.Client.SmartTalentMatching.GetMatches.Queries;

public sealed class GetTalentMatchesQueryHandler(
    IApplicationDbContext context,
    IPremiumAccessService premiumAccess,
    IAiServiceClient aiServiceClient) : IRequestHandler<GetTalentMatchesQuery, TalentMatchingResultDto>
{
    public async Task<TalentMatchingResultDto> Handle(
        GetTalentMatchesQuery query,
        CancellationToken cancellationToken)
    {
        await premiumAccess.RequirePremiumClientAsync(query.UserId, cancellationToken);
        var jobExists = await context.Set<JobPost>().AsNoTracking()
            .AnyAsync(x => x.JobPostsId == query.JobPostId && x.Status == 1 &&
                x.ClientProfiles.UserId == query.UserId, cancellationToken);
        if (!jobExists) throw new NotFoundException("Job post not found.");

        TalentMatchingResponseDto aiResult;
        try
        {
            aiResult = await aiServiceClient.RecommendTalentAsync(new TalentMatchingRequestDto
            {
                JobId = query.JobPostId.ToString(),
                TopK = query.TopK
            }, cancellationToken);
        }
        catch (ExternalServiceException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            throw new ExternalServiceException(
                "AI service is temporarily unavailable. Please try again later.", exception);
        }

        var candidates = aiResult.Matches
            .Select(x => new { Match = x, Parsed = Guid.TryParse(x.FreelancerId, out var id), Id = id })
            .Where(x => x.Parsed && x.Match.MatchScore is >= 0 and <= 1)
            .ToList();
        var ids = candidates.Select(x => x.Id).Distinct().ToList();
        var profiles = await context.Set<FreelancerProfile>().AsNoTracking()
            .Where(x => ids.Contains(x.FreelancerProfilesId) && x.User.IsActive &&
                (x.Availability == 0 || x.Availability == 1))
            .Select(x => new
            {
                x.FreelancerProfilesId,
                x.Title,
                DisplayName = x.User.FullName
            })
            .ToDictionaryAsync(x => x.FreelancerProfilesId, cancellationToken);

        var matches = candidates
            .Where(x => profiles.ContainsKey(x.Id))
            .Select(x =>
            {
                var profile = profiles[x.Id];
                return new TalentMatchDto(
                    x.Id,
                    profile.DisplayName ?? "Freelancer",
                    profile.Title,
                    Math.Round((decimal)x.Match.MatchScore * 100m, 2),
                    x.Match.SkillsMatched,
                    x.Match.SkillsMissing,
                    x.Match.MatchReasons);
            })
            .OrderByDescending(x => x.MatchPercentage)
            .Take(query.TopK)
            .ToList();
        if (matches.Count == 0)
            throw new NotFoundException("No matching freelancers found. Try adjusting your criteria.");
        return new TalentMatchingResultDto(query.JobPostId, matches);
    }
}
