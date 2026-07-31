using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class Review
{
    public Guid ReviewsId { get; set; }

    public Guid ContractsId { get; set; }

    public Guid ReviewerId { get; set; }

    public Guid RevieweeId { get; set; }

    public int Rating { get; set; }

    public string? Comment { get; set; }

    public int? CommunicationRating { get; set; }

    public int? QualityRating { get; set; }

    public int? TimelinessRating { get; set; }

    public bool? IsVisible { get; set; }

    public int ModerationStatus { get; set; }

    public Guid? ModeratedByAdminId { get; set; }

    public DateTime? ModeratedAt { get; set; }

    public string? ModerationNote { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Contract Contracts { get; set; } = null!;

    public virtual User Reviewee { get; set; } = null!;

    public virtual User Reviewer { get; set; } = null!;
}
