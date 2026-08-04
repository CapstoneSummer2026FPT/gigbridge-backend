using Application.Features.Profiles.FreelancerProfile.GetFreelancerProfile.DTOs;
using MediatR;

namespace Application.Features.Portfolios.GetPortfolioItems.Queries;

public sealed record GetPortfolioItemsQuery(Guid UserId) : IRequest<IReadOnlyList<PortfolioItemDto>>;
