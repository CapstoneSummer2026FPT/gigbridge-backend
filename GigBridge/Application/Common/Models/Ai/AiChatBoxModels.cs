using System.Collections.Generic;

namespace Application.Common.Models.Ai;

public class AiChatBoxRequestDto
{
    public string Question { get; set; } = string.Empty;
    public List<AiChatBoxMessageDto> History { get; set; } = new();
    public string CollectionName { get; set; } = "general-knowledge";
    public string Style { get; set; } = "precision";
}

public class AiChatBoxMessageDto
{
    public string Role { get; set; } = string.Empty; // user, assistant
    public string Content { get; set; } = string.Empty;
}

public class AiChatBoxResponseDto
{
    public string Answer { get; set; } = string.Empty;
}
