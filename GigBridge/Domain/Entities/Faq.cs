using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class Faq
{
    public int FaqsId { get; set; }

    public int FaqcategoriesId { get; set; }

    public string Question { get; set; } = null!;

    public string? QuestionVi { get; set; }

    public string Answer { get; set; } = null!;

    public string? AnswerVi { get; set; }

    public int? SortOrder { get; set; }

    public bool? IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Faqcategory Faqcategories { get; set; } = null!;
}
