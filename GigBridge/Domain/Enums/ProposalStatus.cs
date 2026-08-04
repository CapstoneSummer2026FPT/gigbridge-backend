namespace Domain.Enums;

public enum ProposalStatus
{
    Draft = 0,
    Pending = 1,
    Shortlisted = 2,
    Accepted = 3,
    Rejected = 4,
    Withdrawn = 5
}

public enum ProposalModerationStatus
{
    Active = 0,
    Invalidated = 1
}
