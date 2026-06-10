using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class Category
{
    public Guid CategoriesId { get; set; }

    public string Name { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public int? SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<Category> InverseParentCategory { get; set; } = new List<Category>();

    public virtual ICollection<JobPost> JobPosts { get; set; } = new List<JobPost>();

    public virtual Category? ParentCategory { get; set; }

    public virtual ICollection<PortfolioItem> PortfolioItems { get; set; } = new List<PortfolioItem>();

    public virtual ICollection<Skill> Skills { get; set; } = new List<Skill>();
}
