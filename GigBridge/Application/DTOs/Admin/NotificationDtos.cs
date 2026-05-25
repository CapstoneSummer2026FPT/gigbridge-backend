namespace Application.DTOs.Admin;

public sealed class SystemAlertRequestDto
{
    public List<Guid> UserIds { get; set; } = [];
    public string? Title { get; set; }
    public string? Content { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? ReferenceType { get; set; }
}

public sealed class SystemAlertResultDto
{
    public int RecipientCount { get; init; }
}
