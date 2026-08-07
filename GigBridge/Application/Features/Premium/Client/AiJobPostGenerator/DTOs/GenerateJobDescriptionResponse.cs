using System;
using System.Collections.Generic;

namespace Application.Features.Premium.Client.AiJobPostGenerator.DTOs;

public class GeneratedSkillDto
{
    public Guid SkillsId { get; set; }
    public string Name { get; set; } = null!;
}

public class GeneratedJobPostMilestoneDto
{
    public string Title { get; set; } = null!;
    public decimal Amount { get; set; }
    public string EstimatedDuration { get; set; } = null!;
    public string DueDate { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Deliverables { get; set; } = null!;
    public string AcceptanceCriteria { get; set; } = null!;
}

public class GenerateJobDescriptionDetailsResponse
{
    public string Title { get; set; } = null!;
    public Guid? MajorId { get; set; }
    public string? MajorName { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public Guid? MajorCategoryId { get; set; }
    public List<GeneratedSkillDto> Skills { get; set; } = new();
    public List<string> CustomSkills { get; set; } = new();
    public string Description { get; set; } = null!;
    public decimal? BudgetMin { get; set; }
    public decimal? BudgetMax { get; set; }
    public string? EstimatedDuration { get; set; }
    public string AiDisclaimer { get; set; } = string.Empty;
}

public class GenerateJobHiringPlanResponse
{
    public List<string> QuestionRecruitment { get; set; } = new();
    public List<GeneratedJobPostMilestoneDto> Milestones { get; set; } = new();
}
