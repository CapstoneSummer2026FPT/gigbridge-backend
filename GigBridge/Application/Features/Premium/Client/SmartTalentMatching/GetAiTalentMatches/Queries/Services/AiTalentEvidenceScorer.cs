using System;
using System.Collections.Generic;
using System.Linq;
using Application.Features.Premium.Client.SmartTalentMatching.Common.Services;

namespace Application.Features.Premium.Client.SmartTalentMatching.GetAiTalentMatches.Queries.Services;

public sealed record AiTalentEvidenceScore(
    decimal Score,
    decimal DataCoverage,
    IReadOnlyList<string> MatchedSkills,
    IReadOnlyList<string> MissingSkills,
    IReadOnlyList<string> Reasons);

public static class AiTalentEvidenceScorer
{
    private const decimal BayesianPriorRating = 4m;
    private const decimal BayesianPriorCount = 5m;

    public static AiTalentEvidenceScore Score(
        AiTalentMatchingJob job,
        AiTalentMatchingCandidate candidate)
    {
        var declaredSkillIds = candidate.Skills.Select(skill => skill.SkillId).ToHashSet();
        var verifiedSkillIds = candidate.VerifiedWork.SelectMany(work => work.Skills)
            .Select(skill => skill.SkillId).ToHashSet();
        var effectiveSkillIds = declaredSkillIds.Concat(verifiedSkillIds).ToHashSet();
        var matched = job.Skills.Where(skill => effectiveSkillIds.Contains(skill.SkillId)).ToList();
        var missing = job.Skills.Where(skill => !effectiveSkillIds.Contains(skill.SkillId)).ToList();

        var components = new List<(decimal Weight, decimal Score)>();
        if (job.Skills.Count > 0)
        {
            components.Add((40m, 100m * matched.Count / job.Skills.Count));
        }

        var exactCategory = job.MajorCategoryId.HasValue &&
            (candidate.MajorCategoryIds.Contains(job.MajorCategoryId.Value) ||
             candidate.VerifiedWork.Any(work => work.MajorCategoryId == job.MajorCategoryId));
        var sameMajor = !exactCategory && job.MajorId.HasValue &&
            (candidate.MajorId == job.MajorId ||
             candidate.VerifiedWork.Any(work => work.MajorId == job.MajorId));
        if (job.MajorCategoryId.HasValue || job.MajorId.HasValue)
        {
            components.Add((20m, exactCategory ? 100m : sameMajor ? 60m : 0m));
        }

        var bayesianRating = CalculateBayesianRating(candidate.AverageRating, candidate.ReviewCount);
        components.Add((15m, bayesianRating / 5m * 100m));
        components.Add((10m, Math.Min(candidate.CompletedContractCount, 5) / 5m * 100m));
        components.Add((10m, Math.Clamp(candidate.EloPoints, 0, 500) / 500m * 100m));
        components.Add((5m, candidate.Availability == 0 ? 100m : 60m));

        var totalWeight = components.Sum(component => component.Weight);
        var evidenceScore = totalWeight == 0m
            ? 0m
            : components.Sum(component => component.Weight * component.Score) / totalWeight;

        var dataCoverage = 0m;
        if (!string.IsNullOrWhiteSpace(candidate.Title)) dataCoverage += 15m;
        if (!string.IsNullOrWhiteSpace(candidate.Bio)) dataCoverage += 25m;
        if (candidate.MajorId.HasValue || candidate.MajorCategoryIds.Count > 0) dataCoverage += 20m;
        if (candidate.Skills.Count > 0) dataCoverage += 25m;
        if (candidate.VerifiedWork.Count > 0) dataCoverage += 15m;

        var reasons = new List<string>();
        if (job.Skills.Count > 0)
        {
            reasons.Add($"{matched.Count}/{job.Skills.Count} canonical job skills matched");
        }
        if (exactCategory) reasons.Add("Exact category alignment");
        else if (sameMajor) reasons.Add("Related major alignment");
        if (candidate.CompletedContractCount > 0)
            reasons.Add($"{candidate.CompletedContractCount} completed GigBridge contract(s)");
        if (candidate.ReviewCount > 0)
            reasons.Add($"{candidate.AverageRating:F1} rating from {candidate.ReviewCount} review(s)");

        return new AiTalentEvidenceScore(
            Round(evidenceScore),
            Round(dataCoverage),
            matched.Select(skill => skill.Name).OrderBy(name => name).ToList(),
            missing.Select(skill => skill.Name).OrderBy(name => name).ToList(),
            reasons);
    }

    private static decimal CalculateBayesianRating(double averageRating, int reviewCount)
    {
        if (reviewCount <= 0) return BayesianPriorRating;
        var count = Math.Max(reviewCount, 0);
        return (Math.Clamp((decimal)averageRating, 0m, 5m) * count +
                BayesianPriorRating * BayesianPriorCount) /
            (count + BayesianPriorCount);
    }

    private static decimal Round(decimal value) => Math.Round(value, 2);
}
