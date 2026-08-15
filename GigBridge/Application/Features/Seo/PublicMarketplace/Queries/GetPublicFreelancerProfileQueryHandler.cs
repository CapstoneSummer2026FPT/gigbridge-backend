using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Profiles.FreelancerProfile.GetFreelancerProfile.Queries;
using Application.Features.Seo.PublicMarketplace.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Seo.PublicMarketplace.Queries;

public sealed class GetPublicFreelancerProfileQueryHandler
    : IRequestHandler<GetPublicFreelancerProfileQuery, PublicFreelancerProfileDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IMediator _mediator;

    public GetPublicFreelancerProfileQueryHandler(IApplicationDbContext context, IMediator mediator)
    {
        _context = context;
        _mediator = mediator;
    }

    public async Task<PublicFreelancerProfileDto> Handle(
        GetPublicFreelancerProfileQuery request,
        CancellationToken cancellationToken)
    {
        var isPublic = await _context.Set<Domain.Entities.FreelancerProfile>()
            .AsNoTracking()
            .AnyAsync(
                profile => profile.UserId == request.UserId
                    && profile.User.IsActive,
                cancellationToken);

        if (!isPublic)
        {
            throw new NotFoundException("Public freelancer profile does not exist.");
        }

        var detail = await _mediator.Send(
            new GetFreelancerProfileQuery(request.UserId),
            cancellationToken);

        return new PublicFreelancerProfileDto
        {
            FreelancerProfilesId = detail.FreelancerProfilesId,
            UserId = detail.UserId,
            Title = detail.Title,
            Bio = detail.Bio,
            Availability = detail.Availability,
            Location = detail.Location,
            CreatedAt = detail.CreatedAt,
            UpdatedAt = detail.UpdatedAt,
            MajorId = detail.MajorId,
            MajorName = detail.MajorName,
            UserFullName = detail.UserFullName,
            UserAvatar = detail.UserAvatar,
            Rating = detail.Rating,
            EloPoints = detail.EloPoints,
            IsPremium = detail.IsPremium,
            IsIdentityVerified = detail.IsIdentityVerified,
            ShowProVerifiedBadge = detail.ShowProVerifiedBadge,
            AllowSearchEngineIndexing = detail.AllowSearchEngineIndexing,
            Categories = detail.Categories,
            Skills = detail.Skills,
            PortfolioItems = detail.PortfolioItems,
            WorkExperiences = detail.WorkExperiences
        };
    }
}
