using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class PortfolioItem
{
    public Guid PortfolioItemsId { get; set; }

    public Guid FreelancerId { get; set; }

    public string? ProjectUrl { get; set; }



    public virtual FreelancerProfile Freelancer { get; set; } = null!;
}
