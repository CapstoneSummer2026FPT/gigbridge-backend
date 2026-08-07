using Application.Features.Elo.DTOs;
using MediatR;

namespace Application.Features.Admin.Elo.Queries.GetEloPolicy;

public sealed record GetEloPolicyQuery : IRequest<EloPolicyDto>;
