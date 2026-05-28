namespace Application.DTOs.Admin;

public class DisputeDto
{
    public Guid DisputeId { get; init; }
    public Guid ContractId { get; init; }
    public Guid InitiatorId { get; init; }
    public Guid? MilestoneId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public int Status { get; init; }
    public int? Resolution { get; init; }
    public string? ResolutionNote { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class DisputeDetailDto : DisputeDto
{
    public DisputeContractDto Contract { get; init; } = null!;
    public DisputeMilestoneDto? Milestone { get; init; }
    public IReadOnlyList<DisputeEvidenceDto> Evidence { get; init; } = [];
    public IReadOnlyList<DisputeMessageDto> Messages { get; init; } = [];
    public IReadOnlyList<PaymentProofDto> PaymentProofs { get; init; } = [];
}

public sealed class DisputeContractDto
{
    public Guid ContractId { get; init; }
    public string Title { get; init; } = string.Empty;
    public decimal TotalBudget { get; init; }
    public int Status { get; init; }
}

public sealed class DisputeMilestoneDto
{
    public Guid MilestoneId { get; init; }
    public string Title { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public int Status { get; init; }
}

public sealed class DisputeEvidenceDto
{
    public Guid DisputeEvidenceId { get; init; }
    public Guid UploadedById { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string FileUrl { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class DisputeMessageDto
{
    public Guid DisputeMessageId { get; init; }
    public Guid SenderId { get; init; }
    public string Content { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

public sealed class PaymentProofDto
{
    public Guid PaymentProofId { get; init; }
    public Guid UploadedById { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string FileUrl { get; init; } = string.Empty;
    public string? Note { get; init; }
    public int Status { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class DisputeReviewRequestDto
{
    public string? AdminNote { get; set; }
}

public sealed class DisputeResolutionRequestDto
{
    public int Resolution { get; set; }
    public string? ResolutionNote { get; set; }
}
