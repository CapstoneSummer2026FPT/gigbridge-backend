using System;
using System.Collections.Generic;

namespace Application.Features.Auth.Shared.DTOs
{
    public class EmailRequest
    {
        public string To { get; set; } = default!;
        public string Subject { get; set; } = default!;
        public string Body { get; set; } = default!;
        public bool IsHtml { get; set; } = true;
        public List<string>? Attachments { get; set; }
    }
}
