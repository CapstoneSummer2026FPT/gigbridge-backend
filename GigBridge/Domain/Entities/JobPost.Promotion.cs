namespace Domain.Entities;

public partial class JobPost
{
    public bool IsFeatured { get; set; }
    public DateTime? FeaturedFrom { get; set; }
    public DateTime? FeaturedUntil { get; set; }
    public ICollection<JobPostPromotion> JobPostPromotions { get; set; } = new List<JobPostPromotion>();
}
