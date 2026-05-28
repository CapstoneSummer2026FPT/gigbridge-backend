using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Features.Auth.DTOs
{
    public class GoogleUserInfoDTO
    {
        public string Email { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string GoogleId { get; set; } = null!;
        public string? PictureUrl { get; set; }
    }
}
