namespace Application.Features.Auth.GoogleLogin.DTOs
{
    public class GoogleTokenResponse
    {
        public string access_token { get; set; } = null!;
        public string id_token { get; set; } = null!;
        public int expires_in { get; set; }
        public string token_type { get; set; } = null!;
    }
}
