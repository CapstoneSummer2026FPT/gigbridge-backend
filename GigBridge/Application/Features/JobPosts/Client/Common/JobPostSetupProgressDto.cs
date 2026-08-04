namespace Application.Features.JobPosts.Client.Common;

public sealed class JobPostSetupProgressDto
{
    public string NextIncompleteStep { get; set; } = JobPostSetupStepNames.Details;

    public bool IsDetailsComplete { get; set; }

    public Guid? ContractId { get; set; }

    public Guid? ESignDocumentId { get; set; }

    public int? ESignStatus { get; set; }

    public bool HasMilestones { get; set; }

    public bool IsMilestonePlanComplete { get; set; }

    public bool CanPublish { get; set; }
}

public static class JobPostSetupStepNames
{
    public const string Details = "Details";
    public const string ESign = "ESign";
    public const string Milestones = "Milestones";
    public const string ReadyToPublish = "ReadyToPublish";
    public const string Published = "Published";
}
