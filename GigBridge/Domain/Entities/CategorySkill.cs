namespace Domain.Entities;

public partial class CategorySkill
{
    public Guid CategorySkillsId { get; set; }

    public Guid CategoryId { get; set; }

    public Guid SkillId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Category Category { get; set; } = null!;

    public virtual Skill Skill { get; set; } = null!;
}