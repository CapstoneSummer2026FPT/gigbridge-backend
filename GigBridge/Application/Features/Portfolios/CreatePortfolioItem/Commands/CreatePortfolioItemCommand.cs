using Application.Features.Portfolios.Common.DTOs;
using Application.Features.Profiles.FreelancerProfile.GetFreelancerProfile.DTOs;
using MediatR;

namespace Application.Features.Portfolios.CreatePortfolioItem.Commands;

public sealed record CreatePortfolioItemCommand(
    Guid UserId,
    PortfolioItemInputDto Dto,
    PortfolioImageUpload? Image = null)
    : IRequest<PortfolioItemDto>;
