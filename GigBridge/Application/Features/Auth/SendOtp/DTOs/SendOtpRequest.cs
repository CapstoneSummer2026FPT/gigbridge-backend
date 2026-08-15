using Application.Common.InternalServices.Auth.Services;

namespace Application.Features.Auth.SendOtp.DTOs;

public class SendOtpRequest
{
    public string Email { get; set; } = null!;
    public string Purpose { get; set; } = OtpPurposeNames.Signup;
}
