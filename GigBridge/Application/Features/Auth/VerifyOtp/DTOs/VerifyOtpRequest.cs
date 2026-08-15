using Application.Common.InternalServices.Auth.Services;

namespace Application.Features.Auth.VerifyOtp.DTOs;

public class VerifyOtpRequest
{
    public string Email { get; set; } = null!;
    public string Otp { get; set; } = null!;
    public string Purpose { get; set; } = OtpPurposeNames.Signup;
    public string? IdentityOrTaxCode { get; set; }
}
