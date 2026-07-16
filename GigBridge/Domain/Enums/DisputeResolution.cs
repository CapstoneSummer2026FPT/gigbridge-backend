namespace Domain.Enums;

/// <summary>
/// Matches the existing Dispute.Resolution int column in the database.
/// 0=ClientFavored, 1=FreelancerFavored, 2=Split, 3=Dismissed
/// </summary>
public enum DisputeResolution
{
    ClientFavored = 0,
    FreelancerFavored = 1,
    Split = 2,
    Dismissed = 3
}
