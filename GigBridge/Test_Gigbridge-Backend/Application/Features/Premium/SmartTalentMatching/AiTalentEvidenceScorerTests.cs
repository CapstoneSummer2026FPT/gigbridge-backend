using Application.Features.Premium.Client.SmartTalentMatching.GetMatches.Queries;

namespace Test_Gigbridge_Backend.Application.Features.Premium.SmartTalentMatching;

public sealed class AiTalentEvidenceScorerTests
{
    private static readonly Guid JobSkillId = Guid.NewGuid();
    private static readonly Guid CategoryId = Guid.NewGuid();
    private static readonly Guid MajorId = Guid.NewGuid();

    [Fact]
    public void Score_NoDeclaredSkills_RemainsScorableAndReportsSkillGap()
    {
        var result = AiTalentEvidenceScorer.Score(CreateJob(), CreateCandidate(skills: []));

        Assert.InRange(result.Score, 0m, 100m);
        Assert.Empty(result.MatchedSkills);
        Assert.Equal(["C#"], result.MissingSkills);
        Assert.Equal(60m, result.DataCoverage);
    }

    [Fact]
    public void Score_CanonicalSkillAndVerifiedEvidenceIncreaseStructuredScore()
    {
        var baseline = AiTalentEvidenceScorer.Score(CreateJob(), CreateCandidate(skills: []));
        var enriched = AiTalentEvidenceScorer.Score(
            CreateJob(),
            CreateCandidate(
                skills: [new AiTalentSkill(JobSkillId, "C#")],
                completedContracts: 5,
                rating: 5,
                reviewCount: 10));

        Assert.True(enriched.Score > baseline.Score);
        Assert.Equal(["C#"], enriched.MatchedSkills);
        Assert.Empty(enriched.MissingSkills);
        Assert.Contains(enriched.Reasons, reason => reason.Contains("completed GigBridge contract"));
    }

    [Fact]
    public void Score_JobWithoutSkillsOrTaxonomy_RenormalizesRemainingEvidence()
    {
        var job = CreateJob() with
        {
            MajorId = null,
            MajorCategoryId = null,
            Skills = []
        };

        var result = AiTalentEvidenceScorer.Score(job, CreateCandidate(skills: []));

        Assert.Equal(47.5m, result.Score);
        Assert.Empty(result.MatchedSkills);
        Assert.Empty(result.MissingSkills);
    }

    private static AiTalentMatchingJob CreateJob() => new(
        Guid.NewGuid(),
        "Backend Engineer",
        "Build APIs",
        "Technology",
        MajorId,
        "Software Engineering",
        CategoryId,
        "Backend Development",
        [new AiTalentSkill(JobSkillId, "C#")],
        [],
        null,
        null);

    private static AiTalentMatchingCandidate CreateCandidate(
        IReadOnlyList<AiTalentSkill> skills,
        int completedContracts = 0,
        double rating = 0,
        int reviewCount = 0) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        "Test Freelancer",
        null,
        "Backend Developer",
        "Builds APIs",
        null,
        0,
        MajorId,
        "Software Engineering",
        new HashSet<Guid> { CategoryId },
        ["Backend Development"],
        skills,
        completedContracts,
        [],
        100,
        rating,
        reviewCount);
}
