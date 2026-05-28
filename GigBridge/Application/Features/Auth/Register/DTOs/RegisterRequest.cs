using System.ComponentModel.DataAnnotations;

namespace Application.Features.Auth.Register.DTOs
{
    public class RegisterRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;

        public string? FullName { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [RegularExpression(
            @"^(?=\S{8,}$)(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).*$"
        )]
        public string Password { get; set; } = null!;

        [Required]
        [Compare("Password", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = null!;

        public int role { get; set; }
    }
}
