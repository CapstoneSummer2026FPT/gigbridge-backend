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
    string? Description = null,
    SubmitMilestoneFile? File = null,
    string? ExternalUrl = null) : IRequest<ContractMilestoneResponse>
{
    public SubmitMilestoneCommand(Guid contractId, Guid milestoneId, Guid userId)
        : this(contractId, milestoneId, userId, null, null, null)
    {
    }
}
