using System.IO;

namespace Application.Features.Contracts.Milestones.WorkItems.Common.DTOs;

public sealed record WorkItemUploadFile(
    Stream Content,
    string FileName,
    string ContentType,
    long Length);

/// <summary>One work item inside a submission batch, with the files that constitute its delivery.</summary>
public sealed record SubmitWorkItemEntry(
    Guid WorkItemId,
    string? Note,
    IReadOnlyList<WorkItemUploadFile> Files);

/// <summary>
/// Client request body for bulk approve. <c>WorkItemIds</c> may be in any order and need not cover
/// every work item in the milestone — partial review is the normal case.
/// </summary>
public sealed record ApproveWorkItemsRequest(IReadOnlyList<Guid> WorkItemIds);

public sealed record RequestWorkItemRevisionRequest(IReadOnlyList<Guid> WorkItemIds, string Reason);
