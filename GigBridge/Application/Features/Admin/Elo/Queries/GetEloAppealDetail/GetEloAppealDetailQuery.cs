using Application.Features.Admin.Elo.DTOs;
using MediatR;

namespace Application.Features.Admin.Elo.Queries.GetEloAppealDetail;

public sealed record GetEloAppealDetailQuery(Guid AppealId) : IRequest<AdminEloAppealDetailDto>;
