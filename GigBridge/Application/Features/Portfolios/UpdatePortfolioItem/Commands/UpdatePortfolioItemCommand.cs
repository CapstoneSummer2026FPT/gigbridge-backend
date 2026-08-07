using Application.Features.Portfolios.Common.DTOs;
using Application.Features.Profiles.FreelancerProfile.GetFreelancerProfile.DTOs;
using MediatR;

namespace Application.Features.Portfolios.UpdatePortfolioItem.Commands;

public sealed record UpdatePortfolioItemCommand(
    Guid UserId,
    Guid PortfolioItemId,
    PortfolioItemInputDto Dto,
    PortfolioImageUpload? Image = null,
    bool RemoveImage = false,
    bool PreserveExistingImage = false) : IRequest<PortfolioItemDto>;
