using Application.Features.Seo.PublicMarketplace.DTOs;
using MediatR;

namespace Application.Features.Seo.PublicMarketplace.Queries;

public sealed record GetPublicJobPostDetailQuery(Guid JobPostId)
    : IRequest<PublicJobPostDetailDto>;
