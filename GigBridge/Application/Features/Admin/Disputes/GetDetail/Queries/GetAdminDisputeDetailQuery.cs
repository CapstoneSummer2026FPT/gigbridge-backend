using Application.Features.Admin.Disputes.Common.DTOs;
using MediatR;

namespace Application.Features.Admin.Disputes.GetDetail.Queries;

public sealed record GetAdminDisputeDetailQuery(Guid DisputeId) : IRequest<AdminDisputeDetailResponse>;
