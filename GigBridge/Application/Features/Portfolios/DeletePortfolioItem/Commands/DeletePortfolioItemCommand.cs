using MediatR;

namespace Application.Features.Portfolios.DeletePortfolioItem.Commands;

public sealed record DeletePortfolioItemCommand(Guid UserId, Guid PortfolioItemId) : IRequest<bool>;
