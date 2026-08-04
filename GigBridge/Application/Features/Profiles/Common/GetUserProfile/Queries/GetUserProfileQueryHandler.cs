using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Profiles.Common.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Profiles.Common.GetUserProfile.Queries;

public sealed class GetUserProfileQueryHandler
    : IRequestHandler<GetUserProfileQuery, PublicUserProfileDto>
{
    private readonly IApplicationDbContext _context;

    public GetUserProfileQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PublicUserProfileDto> Handle(
        GetUserProfileQuery request,
        CancellationToken cancellationToken)
    {
        var profile = await _context.Set<User>()
            .AsNoTracking()
            .Where(user => user.UserId == request.UserId)
            .Select(user => new PublicUserProfileDto
            {
                UserId = user.UserId,
                FullName = user.FullName,
                Avatar = user.Avatar,
                Role = user.Role
            })
            .FirstOrDefaultAsync(cancellationToken);

        return profile ?? throw new NotFoundException(nameof(User), request.UserId);
    }
}
