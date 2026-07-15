namespace Domain.Entities;

public partial class MajorCategory
{
    public Guid MajorCategoriesId { get; set; }

    public Guid MajorId { get; set; }

    public Guid CategoryId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Major Major { get; set; } = null!;

    public virtual Category Category { get; set; } = null!;

    public virtual ICollection<JobPost> JobPosts { get; set; } = new List<JobPost>();

    public virtual ICollection<FreelancerProfileCategory> FreelancerProfileCategories { get; set; } = new List<FreelancerProfileCategory>();
}
