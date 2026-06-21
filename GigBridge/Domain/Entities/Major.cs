namespace Domain.Entities;

public partial class Major
{
    public Guid MajorsId { get; set; }

    public string Name { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public int? SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<MajorCategory> MajorCategories { get; set; } = new List<MajorCategory>();
}