using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class JobPostQuestion
{
    public Guid JobPostQuestionsId { get; set; }

    public Guid JobPostsId { get; set; }

    public string QuestionText { get; set; } = null!;

    public int OrderIndex { get; set; }

    public bool IsRequired { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual JobPost JobPosts { get; set; } = null!;

    public virtual ICollection<ProposalAnswer> ProposalAnswers { get; set; } = new List<ProposalAnswer>();
}