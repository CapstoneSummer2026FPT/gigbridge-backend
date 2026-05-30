using System.ComponentModel.DataAnnotations;
using Domain.Enums;

namespace Application.Features.Auth.Register.DTOs
{
    public class RegisterRequest
    {
        
        public string Email { get; set; } = null!;

        public string? FullName { get; set; }

       
        public string Password { get; set; } = null!;

        public string ConfirmPassword { get; set; } = null!;

        public UserRole? role { get; set; }
    }
}
