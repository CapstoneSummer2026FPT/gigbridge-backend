namespace Application.Features.Profiles.Common.UpdateUserProfile.DTOs;

public sealed class UpdateUserProfileDto
{
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? Avatar { get; set; }
    public string? PhoneNumber { get; set; }
    public string? IdentityOrTaxCode { get; set; }
    public string? IdentityVerificationTicket { get; set; }
    public string? PreferredLanguage { get; set; }
}
