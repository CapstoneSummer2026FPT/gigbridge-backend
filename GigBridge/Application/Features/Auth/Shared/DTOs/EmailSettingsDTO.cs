using System;

namespace Application.Features.Auth.Shared.DTOs
{
    public class EmailSettingsDTO
    {
        public string Host { get; set; } = null!;
        public int Port { get; set; }
        public string Username { get; set; } = null!;
        public string Password { get; set; } = null!;
        public bool EnableSSL { get; set; }
    }
}
