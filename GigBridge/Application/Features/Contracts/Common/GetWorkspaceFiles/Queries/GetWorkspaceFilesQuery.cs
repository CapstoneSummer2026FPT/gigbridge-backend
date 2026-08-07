using Application.Features.Contracts.Common.GetWorkspaceFiles.DTOs;
using MediatR;

namespace Application.Features.Contracts.Common.GetWorkspaceFiles.Queries;

public record GetWorkspaceFilesQuery(
    Guid ContractId,
    Guid UserId) : IRequest<IReadOnlyList<WorkspaceFileResponse>>;
