using System;
using System.Collections.Generic;
using System.Linq;
using Application.Features.Premium.Client.SmartTalentMatching.GetTalentMatches.DTOs;

namespace Application.Features.Premium.Client.SmartTalentMatching.GetTalentMatches.Queries.Services;

public sealed record TalentScoringSkill(Guid SkillId, string Name, bool IsRequired,
    int? ProficiencyLevel = null, int? YearsOfExperience = null);
public sealed record TalentScoringWorkExperience(string Title, string CompanyName, string? Description);
public sealed record TalentVerifiedContractEvidence(Guid ContractId, Guid? MajorCategoryId,
    Guid? MajorId, IReadOnlySet<Guid> SkillIds);
public sealed record TalentScoringJob(Guid JobPostId, Guid? MajorCategoryId, Guid? MajorId,
    IReadOnlyList<TalentScoringSkill> Skills, IReadOnlyList<string> CustomSkills);
public sealed record TalentScoringCandidate(Guid FreelancerProfileId, Guid UserId,
    string DisplayName, string? Title, string? Bio, int Availability,
    int ProfileCompletionScore, int EloPoints, double AverageRating, int ReviewCount,
    int CompletedContractCount, Guid? MajorId, IReadOnlySet<Guid> MajorCategoryIds,
    IReadOnlyList<TalentScoringSkill> Skills,
    IReadOnlyList<TalentScoringWorkExperience> WorkExperiences,
    IReadOnlyList<TalentVerifiedContractEvidence> VerifiedContracts);
public sealed record TalentScoredCandidate(TalentMatchDto Match,
    decimal StructuredSkillCoverage, int ReviewCount, int EloPoints);

public static class TalentMatchScorer
{
    public const decimal RequiredSkillCoverageThreshold = 70m;
    private const decimal BayesianPriorRating = 4m;
    private const decimal BayesianPriorReviewCount = 5m;

    public static TalentScoredCandidate? Score(TalentScoringJob job, TalentScoringCandidate candidate)
    {
        var declaredSkills = candidate.Skills.GroupBy(skill => skill.SkillId)
            .ToDictionary(group => group.Key, group => group.First());
        var verifiedSkillIds = candidate.VerifiedContracts.SelectMany(item => item.SkillIds).ToHashSet();
        var effectiveSkillIds = declaredSkills.Keys.Concat(verifiedSkillIds).ToHashSet();
        var required = job.Skills.Where(skill => skill.IsRequired).ToList();
        var requiredMatched = required.Where(skill => effectiveSkillIds.Contains(skill.SkillId)).ToList();
        var requiredCoverage = required.Count == 0 ? 100m : 100m * requiredMatched.Count / required.Count;
        var singleRequiredSatisfied = required.Count != 1 || requiredMatched.Count == 1;
        var meetsRequiredGate = requiredCoverage >= RequiredSkillCoverageThreshold && singleRequiredSatisfied;
        if (!meetsRequiredGate) return null;

        var matched = job.Skills.Where(skill => effectiveSkillIds.Contains(skill.SkillId)).ToList();
        var exactCategory = job.MajorCategoryId.HasValue &&
            (candidate.MajorCategoryIds.Contains(job.MajorCategoryId.Value) ||
             candidate.VerifiedContracts.Any(item => item.MajorCategoryId == job.MajorCategoryId));
        var sameMajor = !exactCategory && job.MajorId.HasValue &&
            (candidate.MajorId == job.MajorId || candidate.VerifiedContracts.Any(item => item.MajorId == job.MajorId));
        if (required.Count == 0 && job.Skills.Count > 0 && matched.Count == 0 && !exactCategory && !sameMajor)
            return null;
        if (job.Skills.Count == 0 && !exactCategory && !sameMajor) return null;

        var totalSkillWeight = job.Skills.Sum(skill => skill.IsRequired ? 2m : 1m);
        var matchedSkillWeight = matched.Sum(skill => skill.IsRequired ? 2m : 1m);
        var coverageScore = totalSkillWeight == 0 ? 0m : 32m * matchedSkillWeight / totalSkillWeight;
        var expertiseScore = CalculateExpertise(matched, declaredSkills);
        var jobSkillIds = job.Skills.Select(skill => skill.SkillId).ToHashSet();
        var verifiedSkillContracts = candidate.VerifiedContracts.Count(contract => contract.SkillIds.Overlaps(jobSkillIds));
        var verifiedSkillScore = job.Skills.Count == 0 ? 0m :
            5m * matched.Count(skill => verifiedSkillIds.Contains(skill.SkillId)) / job.Skills.Count;
        var structuredSkillFit = coverageScore + expertiseScore + verifiedSkillScore;
        var ratingScore = candidate.ReviewCount > 0
            ? Math.Clamp((decimal)candidate.AverageRating * 3m, 0m, 15m)
            : 10m;
        var eloScore = Math.Clamp((decimal)candidate.EloPoints, 0m, 1000m) / 100m;
        var budgetScore = 15m;
        var availabilityScore = candidate.Availability == 0 ? 10m : candidate.Availability == 1 ? 5m : 0m;
        var total = Round(Math.Clamp(ratingScore + eloScore + budgetScore + availabilityScore, 0m, 50m));

        var matchedNames = matched.Select(skill => skill.Name).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        var missingNames = job.Skills.Where(skill => !effectiveSkillIds.Contains(skill.SkillId))
            .Select(skill => skill.Name).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        var verifiedNames = job.Skills.Where(skill => verifiedSkillIds.Contains(skill.SkillId))
            .Select(skill => skill.Name).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        var categoryContracts = candidate.VerifiedContracts.Count(contract =>
            job.MajorCategoryId.HasValue && contract.MajorCategoryId == job.MajorCategoryId);
        var reasons = BuildReasons(requiredMatched.Count, required.Count, requiredCoverage, matched.Count,
            job.Skills.Count, exactCategory, sameMajor, verifiedSkillContracts, verifiedNames,
            candidate.CompletedContractCount, candidate.AverageRating, candidate.ReviewCount, candidate.Availability);
        var confidence = verifiedSkillContracts > 0 && requiredCoverage == 100m ? "high" :
            requiredCoverage >= RequiredSkillCoverageThreshold && (exactCategory || sameMajor) ? "medium" : "low";
        var breakdown = new TalentMatchScoreBreakdownDto(
            StructuredSkillFit: Round(structuredSkillFit),
            SkillCoverage: Round(coverageScore),
            SkillExpertise: Round(expertiseScore),
            VerifiedSkillEvidence: Round(verifiedSkillScore),
            TaxonomyFit: Round(exactCategory ? 15m : sameMajor ? 8m : 0m),
            Reliability: Round(ratingScore),
            BayesianRating: Round(ratingScore),
            CompletedContracts: candidate.CompletedContractCount,
            Availability: Round(availabilityScore),
            ReputationAndProfile: Round(eloScore),
            Elo: Round(eloScore),
            ProfileCompleteness: Round(candidate.ProfileCompletionScore));
        var eligibility = new TalentMatchEligibilityEvidenceDto(Round(requiredCoverage),
            requiredMatched.Count, required.Count, meetsRequiredGate, singleRequiredSatisfied);
        var verifiedWork = new TalentMatchVerifiedWorkEvidenceDto(candidate.CompletedContractCount,
            verifiedSkillContracts, categoryContracts, verifiedNames);
        var match = new TalentMatchDto(candidate.FreelancerProfileId, candidate.UserId,
            candidate.DisplayName, candidate.Title, total, confidence, matchedNames, missingNames,
            job.CustomSkills, reasons, breakdown, eligibility, verifiedWork);
        return new TalentScoredCandidate(match, Round(coverageScore), candidate.ReviewCount, candidate.EloPoints);
    }

    private static decimal CalculateExpertise(IReadOnlyCollection<TalentScoringSkill> matchedJobSkills,
        IReadOnlyDictionary<Guid, TalentScoringSkill> declaredSkills)
    {
        if (matchedJobSkills.Count == 0) return 0m;
        var total = matchedJobSkills.Sum(jobSkill =>
        {
            if (!declaredSkills.TryGetValue(jobSkill.SkillId, out var skill)) return 0m;
            var proficiency = Math.Clamp(skill.ProficiencyLevel ?? 0, 0, 3) / 3m;
            var experience = Math.Clamp(skill.YearsOfExperience ?? 0, 0, 5) / 5m;
            return 0.6m * proficiency + 0.4m * experience;
        });
        return 8m * total / matchedJobSkills.Count;
    }

    private static decimal CalculateBayesianRating(double averageRating, int reviewCount)
    {
        if (reviewCount <= 0) return BayesianPriorRating / 5m * 10m;
        var count = Math.Max(reviewCount, 0);
        var adjusted = (Math.Clamp((decimal)averageRating, 0m, 5m) * count +
            BayesianPriorRating * BayesianPriorReviewCount) / (count + BayesianPriorReviewCount);
        return adjusted / 5m * 10m;
    }

    private static IReadOnlyList<string> BuildReasons(int requiredMatched, int requiredTotal,
        decimal requiredCoverage, int allMatched, int allSkills, bool exactCategory, bool sameMajor,
        int verifiedSkillContracts, IReadOnlyCollection<string> verifiedSkills, int completedContracts,
        double rating, int reviewCount, int availability)
    {
        var reasons = new List<string>();
        if (requiredTotal > 0) reasons.Add($"{requiredMatched}/{requiredTotal} required skill weight ({requiredCoverage:F0}%)");
        else if (allSkills > 0) reasons.Add($"{allMatched}/{allSkills} structured skills matched");
        if (verifiedSkillContracts > 0)
            reasons.Add($"{verifiedSkillContracts} verified contracts using {string.Join(", ", verifiedSkills.Take(3))}");
        if (exactCategory) reasons.Add("Exact category match");
        else if (sameMajor) reasons.Add("Related major match");
        if (completedContracts > 0) reasons.Add($"{completedContracts} completed GigBridge contracts");
        if (reviewCount > 0) reasons.Add($"{rating:F1} rating from {reviewCount} reviews (Bayesian adjusted)");
        reasons.Add(availability == 0 ? "Available full-time" : "Available part-time");
        return reasons;
    }

    private static decimal Round(decimal value) => Math.Round(value, 2);
}
