using Application.Features.Disputes.Common.DTOs;
using MediatR;

namespace Application.Features.Disputes.Admin.GetDisputeDetail.Queries;

public sealed record GetAdminDisputeDetailQuery(Guid DisputeId) : IRequest<AdminDisputeDto>;
