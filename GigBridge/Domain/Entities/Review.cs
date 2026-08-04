using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class Review
{
    public Guid ReviewsId { get; set; }

    public Guid ContractsId { get; set; }

    public Guid ReviewerId { get; set; }

    public Guid RevieweeId { get; set; }

    /// <summary>
    /// Overall rating 1.0–5.0 with one decimal place (computed from the three
    /// criteria sub-ratings). Drives the piecewise Elo calculation.
    /// </summary>
    public decimal Rating { get; set; }

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
