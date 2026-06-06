namespace Application.Features.Auth.ResetPassword.DTOs
{
    public class ResetPasswordRequest
    {
        public string Otp { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string NewPassword { get; set; } = null!;
    }
}
