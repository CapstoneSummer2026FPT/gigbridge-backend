using System;
using System.IO;
using Application.Features.Contracts.Milestones.Common.DTOs;
using MediatR;

namespace Application.Features.Contracts.Milestones.Freelancer.Submit.Commands;

public sealed record SubmitMilestoneFile(
    Stream Content,
    string FileName,
    string ContentType,
    long Length);

public sealed record SubmitMilestoneCommand(
    Guid ContractId,
    Guid MilestoneId,
    Guid UserId,
    string? Description,
    IReadOnlyList<SubmitMilestoneFile> Files) : IRequest<ContractMilestoneResponse>
{
    public SubmitMilestoneCommand(Guid contractId, Guid milestoneId, Guid userId)
        : this(contractId, milestoneId, userId, null, [])
    {
    }

    // Backward-compatible constructor for callers that still submit one multipart file.
    public SubmitMilestoneCommand(
        Guid contractId,
        Guid milestoneId,
        Guid userId,
        string? description = null,
        SubmitMilestoneFile? File = null)
        : this(
            contractId,
            milestoneId,
            userId,
            description,
            File is null ? [] : [File])
    {
    }
}
