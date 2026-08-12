namespace Domain.Enums.Disputes;

/// <summary>
/// Audience selected for a dispute message. Participant-originated messages
/// use their own party audience so only that party and administrators can read them.
/// </summary>
public enum DisputeMessageRecipient
{
    Client = 0,
    Freelancer = 1,
    Both = 2
}
