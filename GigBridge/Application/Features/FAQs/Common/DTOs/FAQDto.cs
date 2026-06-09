namespace Application.Features.FAQs.Shared.DTOs;

public sealed record FAQDto
{
    public int Id { get; init; }
    public int FaqCategoryId { get; init; }
    public string? FaqCategoryName { get; init; }
    public string Question { get; init; } = string.Empty;
    public string? QuestionVi { get; init; }
    public string Answer { get; init; } = string.Empty;
    public string? AnswerVi { get; init; }
    public int? SortOrder { get; init; }
    public bool? IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
