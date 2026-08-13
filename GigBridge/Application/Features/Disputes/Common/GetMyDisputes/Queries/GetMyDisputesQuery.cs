using Application.Features.Disputes.Common.DTOs;
using MediatR;

namespace Application.Features.Disputes.Common.GetMyDisputes.Queries;

public sealed record GetMyDisputesQuery(Guid UserId, int Page = 1, int PageSize = 20) : IRequest<MyDisputesResponse>;
