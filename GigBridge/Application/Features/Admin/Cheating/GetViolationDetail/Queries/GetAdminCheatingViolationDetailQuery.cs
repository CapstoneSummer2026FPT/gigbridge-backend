using Application.Features.Admin.Cheating.DTOs;
using MediatR;

namespace Application.Features.Admin.Cheating.GetViolationDetail.Queries;

public record GetAdminCheatingViolationDetailQuery(Guid ViolationId) : IRequest<AdminCheatingViolationDetailDto>;
