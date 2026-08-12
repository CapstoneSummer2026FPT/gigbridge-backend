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

    public virtual ICollection<MajorCategory> MajorCategories { get; set; } = new List<MajorCategory>();

    public virtual ICollection<CategorySkill> CategorySkills { get; set; } = new List<CategorySkill>();
}
