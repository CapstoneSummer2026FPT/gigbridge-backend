namespace Domain.Entities;

public class FreelancerProfileCategory
{
    public Guid FreelancerProfileCategoriesId { get; set; }

    public Guid FreelancerProfileId { get; set; }

    public Guid MajorCategoryId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual FreelancerProfile FreelancerProfile { get; set; } = null!;

    public virtual MajorCategory MajorCategory { get; set; } = null!;
}
