using Application.DTOs.Admin;

namespace Application.Features.Admin.Faqs.Dto;

public sealed class FaqPageQueryDto : PagedQueryDto
{
    public Guid? CategoryId { get; set; }
    public bool? IsActive { get; set; }
}

public sealed class FaqDto
{
    public Guid FaqId { get; init; }
    public Guid CategoryId { get; init; }
    public string? CategoryName { get; init; }
    public string Question { get; init; } = string.Empty;
    public string? QuestionVi { get; init; }
    public string Answer { get; init; } = string.Empty;
    public string? AnswerVi { get; init; }
    public int? SortOrder { get; init; }
    public bool? IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class SaveFaqRequestDto
{
    public Guid CategoryId { get; set; }
    public string? Question { get; set; }
    public string? QuestionVi { get; set; }
    public string? Answer { get; set; }
    public string? AnswerVi { get; set; }
    public int? SortOrder { get; set; }
    public bool? IsActive { get; set; }
}
