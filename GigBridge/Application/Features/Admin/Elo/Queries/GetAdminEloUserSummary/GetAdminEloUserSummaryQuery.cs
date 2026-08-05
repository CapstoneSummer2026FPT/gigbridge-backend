using Application.Features.Admin.Elo.DTOs;
using MediatR;

namespace Application.Features.Admin.Elo.Queries.GetAdminEloUserSummary;

public sealed record GetAdminEloUserSummaryQuery(Guid UserId) : IRequest<AdminEloUserSummaryDto>;
