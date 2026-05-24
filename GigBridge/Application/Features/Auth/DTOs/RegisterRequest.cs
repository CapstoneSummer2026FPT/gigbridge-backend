using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Features.Auth.DTOs
{
    public class RegisterRequest
    {
        public string FullName { get; set; } = null!;

        public string Email { get; set; } = null!;

        public string Password { get; set; } = null!;
        public string? PhoneNumber { get; set; }

        /// <summary>
        /// Enum UserRole: 0=Client, 1=Freelancer, 2=Admin
        /// </summary>
        public int Role { get; set; }

    }
}
