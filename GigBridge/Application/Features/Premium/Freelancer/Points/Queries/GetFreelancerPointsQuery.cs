using Application.Features.Premium.Freelancer.Points.DTOs;
using MediatR;

namespace Application.Features.Premium.Freelancer.Points.Queries;

public sealed record GetFreelancerPointsQuery(Guid UserId) : IRequest<FreelancerPointsDto>;
