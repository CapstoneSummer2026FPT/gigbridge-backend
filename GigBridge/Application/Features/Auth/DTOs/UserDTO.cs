using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Features.Auth.DTOs
{
public class UserDTO
{
        public Guid UserId { get; set; }

        public string FullName { get; set; } = null!;

        public string Email { get; set; } = null!;

        public string Password { get; set; } = null!;

        public string? Avatar { get; set; }

        public string? PhoneNumber { get; set; }

        /// <summary>
        /// Enum UserRole: 0=Client, 1=Freelancer, 2=Admin
        /// </summary>
        public int Role { get; set; }

        public bool IsEmailVerified { get; set; }

        public bool IsActive { get; set; }

        public string? PreferredLanguage { get; set; }

        public string? Provider { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

       
    }
}
