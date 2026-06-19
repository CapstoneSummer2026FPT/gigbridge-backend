using System;
using System.Collections.Generic;

namespace Application.Features.JobPosts.Client.GenerateJobDescription.DTOs;

public class GeneratedSkillDto
{
    public Guid SkillsId { get; set; }
    public string Name { get; set; } = null!;
}

public class GenerateJobDescriptionResponse
{
    public string Title { get; set; } = null!;
    public Guid? CategoryId { get; set; }
    public List<GeneratedSkillDto> Skills { get; set; } = new();
    public string Description { get; set; } = null!;
}
