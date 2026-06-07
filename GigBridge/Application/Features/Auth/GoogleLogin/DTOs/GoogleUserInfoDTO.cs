namespace Application.Features.Auth.GoogleLogin.DTOs
{
    public class GoogleUserInfoDTO
    {
        public string Email { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string GoogleId { get; set; } = null!;
        public string? PictureUrl { get; set; }
    }
}
