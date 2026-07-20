using Application.Features.Disputes.Common.DTOs;
using MediatR;

namespace Application.Features.Disputes.Admin.GetDisputes.Queries;

public sealed record GetAdminDisputesQuery : IRequest<IReadOnlyList<AdminDisputeDto>>;
