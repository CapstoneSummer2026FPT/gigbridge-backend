using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Features.Auth.DTOs
{
    public class ResetPasswordRequest
    {
        public string PasswordResetToken { get; set; }
        public string Email { get; set; } = null!;

        public string NewPassword { get; set; } = null!;
    }
}
