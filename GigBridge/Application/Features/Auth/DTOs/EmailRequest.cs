using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Features.Auth.DTOs
{
    public class EmailRequest
    {
        public string To { get; set; } = default!; //default! : Stop warning me about null. I got this.
        public string Subject { get; set; } = default!;
        public string Body { get; set; } = default!;

        // Optional but VERY useful
        public bool IsHtml { get; set; } = true;
        public List<string>? Attachments { get; set; }
    }
}
