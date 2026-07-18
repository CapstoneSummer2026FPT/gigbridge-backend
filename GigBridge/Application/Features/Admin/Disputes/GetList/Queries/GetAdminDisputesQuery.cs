using Application.Features.Admin.Disputes.Common.DTOs;
using Domain.Enums;
using MediatR;

namespace Application.Features.Admin.Disputes.GetList.Queries;

public sealed record GetAdminDisputesQuery(
    int Page = 1,
    int PageSize = 20,
    DisputeStatus? Status = null,
    string? Search = null) : IRequest<AdminDisputeListResponse>;
