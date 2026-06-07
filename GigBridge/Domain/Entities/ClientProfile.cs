using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class ClientProfile
{
    public Guid ClientProfilesId { get; set; }

    public Guid UserId { get; set; }

    public string? CompanyName { get; set; }

    public string? CompanyWebsite { get; set; }

    /// <summary>
    /// Enum CompanySize: 0=Solo, 1=Small, 2=Medium, 3=Large
    /// </summary>
    public int? CompanySize { get; set; }

    public string? Industry { get; set; }

    public string? CompanyDescription { get; set; }

    public string? Location { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<Contract> Contracts { get; set; } = new List<Contract>();

    public virtual ICollection<JobPost> JobPosts { get; set; } = new List<JobPost>();

    public virtual User User { get; set; } = null!;
}
