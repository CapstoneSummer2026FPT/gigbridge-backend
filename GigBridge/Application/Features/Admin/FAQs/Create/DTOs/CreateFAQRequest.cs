namespace Application.Features.Admin.FAQs.Create.DTOs;

public sealed record CreateFAQRequest
{
    public int FaqCategoryId { get; init; }
    public string Question { get; init; } = string.Empty;
    public string? QuestionVi { get; init; }
    public string Answer { get; init; } = string.Empty;
    public string? AnswerVi { get; init; }
    public int? SortOrder { get; init; }
}
