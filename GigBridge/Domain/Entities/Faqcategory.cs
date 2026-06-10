using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class Faqcategory
{
    public int FaqcategoriesId { get; set; }

    public string Name { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public int? SortOrder { get; set; }

    public bool? IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<Faq> Faqs { get; set; } = new List<Faq>();
}
