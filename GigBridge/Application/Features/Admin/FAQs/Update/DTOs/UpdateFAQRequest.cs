namespace Application.Features.Admin.FAQs.Update.DTOs;

public sealed record UpdateFAQRequest
{
    public int? FaqCategoryId { get; init; }
    public string? Question { get; init; }
    public string? QuestionVi { get; init; }
    public string? Answer { get; init; }
    public string? AnswerVi { get; init; }
    public int? SortOrder { get; init; }
    public bool? IsActive { get; init; }
}
