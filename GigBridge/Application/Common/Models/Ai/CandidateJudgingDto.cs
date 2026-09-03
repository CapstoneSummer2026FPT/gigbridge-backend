using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Application.Common.Models.Ai;

public class EvidenceClaimDto
{
    [JsonPropertyName("claim")]
    public string Claim { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("assessment")]
    public string Assessment { get; set; } = string.Empty;
}

public class SubcriteriaScoreWithEvidenceDto
{
    [JsonPropertyName("score")]
    public double Score { get; set; }

    [JsonPropertyName("evidence")]
    public List<EvidenceClaimDto> Evidence { get; set; } = new();
}

public class TechnicalSolutionQualitativeEvalDto
{
    [JsonPropertyName("requirement_alignment")]
    public SubcriteriaScoreWithEvidenceDto RequirementAlignment { get; set; } = new();

    [JsonPropertyName("technical_correctness")]
    public SubcriteriaScoreWithEvidenceDto TechnicalCorrectness { get; set; } = new();

    [JsonPropertyName("architecture_quality")]
    public SubcriteriaScoreWithEvidenceDto ArchitectureQuality { get; set; } = new();

    [JsonPropertyName("implementation_feasibility")]
    public SubcriteriaScoreWithEvidenceDto ImplementationFeasibility { get; set; } = new();

    [JsonPropertyName("edge_cases_security")]
    public SubcriteriaScoreWithEvidenceDto EdgeCasesSecurity { get; set; } = new();
}

public class QuestionAnswerQualitativeEvalDto
{
    [JsonPropertyName("question_index")]
    public int QuestionIndex { get; set; }

    [JsonPropertyName("question_text")]
    public string QuestionText { get; set; } = string.Empty;

    [JsonPropertyName("candidate_answer")]
    public string CandidateAnswer { get; set; } = string.Empty;

    [JsonPropertyName("answer_correctness")]
    public SubcriteriaScoreWithEvidenceDto AnswerCorrectness { get; set; } = new();

    [JsonPropertyName("technical_reasoning")]
    public SubcriteriaScoreWithEvidenceDto TechnicalReasoning { get; set; } = new();

    [JsonPropertyName("relevance")]
    public SubcriteriaScoreWithEvidenceDto Relevance { get; set; } = new();

    [JsonPropertyName("depth")]
    public SubcriteriaScoreWithEvidenceDto Depth { get; set; } = new();

    [JsonPropertyName("practical_examples")]
    public SubcriteriaScoreWithEvidenceDto PracticalExamples { get; set; } = new();
}

public class RequirementFulfillmentItemDto
{
    [JsonPropertyName("requirement")]
    public string Requirement { get; set; } = string.Empty;

    [JsonPropertyName("is_fulfilled")]
    public bool IsFulfilled { get; set; }

    [JsonPropertyName("matched_milestone")]
    public string? MatchedMilestone { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }
}

public class LLMQualitativeEvaluationDto
{
    [JsonPropertyName("technical_solution")]
    public TechnicalSolutionQualitativeEvalDto TechnicalSolution { get; set; } = new();

    [JsonPropertyName("screening_qa")]
    public List<QuestionAnswerQualitativeEvalDto> ScreeningQa { get; set; } = new();

    [JsonPropertyName("requirement_fulfillment")]
    public List<RequirementFulfillmentItemDto> RequirementFulfillment { get; set; } = new();

    [JsonPropertyName("pricing_realism")]
    public SubcriteriaScoreWithEvidenceDto PricingRealism { get; set; } = new();

    [JsonPropertyName("timeline_feasibility")]
    public SubcriteriaScoreWithEvidenceDto TimelineFeasibility { get; set; } = new();

    [JsonPropertyName("milestone_structure")]
    public SubcriteriaScoreWithEvidenceDto MilestoneStructure { get; set; } = new();

    [JsonPropertyName("project_specificity")]
    public SubcriteriaScoreWithEvidenceDto ProjectSpecificity { get; set; } = new();

    [JsonPropertyName("substance_density")]
    public SubcriteriaScoreWithEvidenceDto SubstanceDensity { get; set; } = new();

    [JsonPropertyName("probing_questions")]
    public List<string> ProbingQuestions { get; set; } = new();
}

public class PillarScoresDto
{
    [JsonPropertyName("technical_solution")]
    public double TechnicalSolution { get; set; }

    [JsonPropertyName("screening_qa")]
    public double ScreeningQa { get; set; }

    [JsonPropertyName("financial_value")]
    public double FinancialValue { get; set; }

    [JsonPropertyName("milestone_scope")]
    public double MilestoneScope { get; set; }

    [JsonPropertyName("authenticity_fluff")]
    public double AuthenticityFluff { get; set; }
}

public class DeterministicCalculationsDto
{
    [JsonPropertyName("milestone_total")]
    public double MilestoneTotal { get; set; }

    [JsonPropertyName("proposed_budget")]
    public double ProposedBudget { get; set; }

    [JsonPropertyName("is_milestone_clamped")]
    public bool IsMilestoneClamped { get; set; }

    [JsonPropertyName("savings_ratio")]
    public double SavingsRatio { get; set; }

    [JsonPropertyName("savings_ratio_percent")]
    public double SavingsRatioPercent { get; set; }

    [JsonPropertyName("scope_completeness_percent")]
    public double ScopeCompletenessPercent { get; set; }

    [JsonPropertyName("pillar_scores")]
    public PillarScoresDto PillarScores { get; set; } = new();

    [JsonPropertyName("overall_technical_quality_tq")]
    public double OverallTechnicalQualityTQ { get; set; }

    [JsonPropertyName("quality_interpretation_band")]
    public string QualityInterpretationBand { get; set; } = string.Empty;

    [JsonPropertyName("final_value_score_vs")]
    public double FinalValueScoreVS { get; set; }

    [JsonPropertyName("verdict_badge")]
    public string VerdictBadge { get; set; } = string.Empty;
}

public class JobPostMilestoneInputDto
{
    [JsonPropertyName("order_index")]
    public int OrderIndex { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("amount")]
    public double Amount { get; set; }

    [JsonPropertyName("estimated_duration")]
    public string? EstimatedDuration { get; set; }

    [JsonPropertyName("deliverables")]
    public string? Deliverables { get; set; }
}

public class JobPostBaselineInputDto
{
    [JsonPropertyName("job_id")]
    public string JobId { get; set; } = string.Empty;

    [JsonPropertyName("job_title")]
    public string JobTitle { get; set; } = string.Empty;

    [JsonPropertyName("job_description")]
    public string JobDescription { get; set; } = string.Empty;

    [JsonPropertyName("required_skills")]
    public List<string> RequiredSkills { get; set; } = new();

    [JsonPropertyName("budget_min")]
    public double? BudgetMin { get; set; }

    [JsonPropertyName("budget_max")]
    public double? BudgetMax { get; set; }

    [JsonPropertyName("estimated_duration")]
    public string? EstimatedDuration { get; set; }

    [JsonPropertyName("original_milestones")]
    public List<JobPostMilestoneInputDto> OriginalMilestones { get; set; } = new();

    [JsonPropertyName("vetting_questions")]
    public List<string> VettingQuestions { get; set; } = new();
}

public class ProposalMilestoneInputDto
{
    [JsonPropertyName("order_index")]
    public int OrderIndex { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("amount")]
    public double Amount { get; set; }

    [JsonPropertyName("estimated_duration")]
    public string? EstimatedDuration { get; set; }

    [JsonPropertyName("deliverables")]
    public string? Deliverables { get; set; }
}

public class QuestionAnswerPairInputDto
{
    [JsonPropertyName("question_index")]
    public int QuestionIndex { get; set; }

    [JsonPropertyName("question_text")]
    public string QuestionText { get; set; } = string.Empty;

    [JsonPropertyName("candidate_answer")]
    public string CandidateAnswer { get; set; } = string.Empty;
}

public class ProposalOfferInputDto
{
    [JsonPropertyName("proposal_id")]
    public string ProposalId { get; set; } = string.Empty;

    [JsonPropertyName("freelancer_id")]
    public string FreelancerId { get; set; } = string.Empty;

    [JsonPropertyName("freelancer_name")]
    public string? FreelancerName { get; set; }

    [JsonPropertyName("proposed_budget")]
    public double ProposedBudget { get; set; }

    [JsonPropertyName("proposed_duration")]
    public string? ProposedDuration { get; set; }

    [JsonPropertyName("cover_letter")]
    public string? CoverLetter { get; set; }

    [JsonPropertyName("analysis_summary")]
    public string? AnalysisSummary { get; set; }

    [JsonPropertyName("solution_approach")]
    public string? SolutionApproach { get; set; }

    [JsonPropertyName("edited_milestones")]
    public List<ProposalMilestoneInputDto> EditedMilestones { get; set; } = new();

    [JsonPropertyName("vetting_qa_answers")]
    public List<QuestionAnswerPairInputDto> VettingQaAnswers { get; set; } = new();
}

public class CandidateJudgingRequestDto
{
    [JsonPropertyName("job_post_baseline")]
    public JobPostBaselineInputDto JobPostBaseline { get; set; } = new();

    [JsonPropertyName("candidate_proposal")]
    public ProposalOfferInputDto CandidateProposal { get; set; } = new();
}

public class BatchCandidateJudgingRequestDto
{
    [JsonPropertyName("job_post_baseline")]
    public JobPostBaselineInputDto JobPostBaseline { get; set; } = new();

    [JsonPropertyName("proposals")]
    public List<ProposalOfferInputDto> Proposals { get; set; } = new();

    [JsonPropertyName("batch_chunk_size")]
    public int BatchChunkSize { get; set; } = 1;
}

public class CandidateJudgingResponseDto
{
    [JsonPropertyName("proposal_id")]
    public string ProposalId { get; set; } = string.Empty;

    [JsonPropertyName("job_id")]
    public string JobId { get; set; } = string.Empty;

    [JsonPropertyName("llm_qualitative_evaluation")]
    public LLMQualitativeEvaluationDto LlmQualitativeEvaluation { get; set; } = new();

    [JsonPropertyName("deterministic_calculations")]
    public DeterministicCalculationsDto DeterministicCalculations { get; set; } = new();
}

public class BatchCandidateJudgingResponseDto
{
    [JsonPropertyName("processed_count")]
    public int ProcessedCount { get; set; }

    [JsonPropertyName("total_requested")]
    public int TotalRequested { get; set; }

    [JsonPropertyName("is_completed")]
    public bool IsCompleted { get; set; }

    [JsonPropertyName("judged_proposals")]
    public List<CandidateJudgingResponseDto> JudgedProposals { get; set; } = new();
}
