using Application.Features.Seo.PublicMarketplace.DTOs;
using MediatR;

namespace Application.Features.Seo.PublicMarketplace.Queries;

public sealed record GetSeoSitemapResourcesQuery : IRequest<SeoSitemapResourcesDto>;
