using Application.Features.Seo.PublicMarketplace.DTOs;
using MediatR;

namespace Application.Features.Seo.PublicMarketplace.Queries;

public sealed record GetPublicFreelancerProfileQuery(Guid UserId)
    : IRequest<PublicFreelancerProfileDto>;
