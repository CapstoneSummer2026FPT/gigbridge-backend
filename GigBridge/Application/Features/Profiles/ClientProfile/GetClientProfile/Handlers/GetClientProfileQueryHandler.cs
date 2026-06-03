using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Profiles.ClientProfile.GetClientProfile.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ClientProfileEntity = Domain.Entities.ClientProfile;

namespace Application.Features.Profiles.ClientProfile.GetClientProfile.Handlers;

public class GetClientProfileQueryHandler 
    : IRequestHandler<Queries.GetClientProfileQuery, ClientProfileDetailDto>
{
    private readonly IApplicationDbContext _context;

    public GetClientProfileQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ClientProfileDetailDto> Handle(
        Queries.GetClientProfileQuery request, 
        CancellationToken cancellationToken)
    {
        var clientProfile = await _context.Set<ClientProfileEntity>()
            .AsNoTracking()
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.UserId == request.UserId, cancellationToken);

        if (clientProfile == null)
        {
            throw new NotFoundException("ClientProfile", request.UserId);
        }

        var detailDto = new ClientProfileDetailDto
        {
            ClientProfilesId = clientProfile.ClientProfilesId,
            UserId = clientProfile.UserId,
            CompanyName = clientProfile.CompanyName,
            CompanyWebsite = clientProfile.CompanyWebsite,
            CompanySize = clientProfile.CompanySize,
            Industry = clientProfile.Industry,
            CompanyDescription = clientProfile.CompanyDescription,
            Location = clientProfile.Location,
            CreatedAt = clientProfile.CreatedAt,
            UpdatedAt = clientProfile.UpdatedAt,

            UserFullName = clientProfile.User.FullName,
            UserEmail = clientProfile.User.Email,
            UserAvatar = clientProfile.User.Avatar
        };

        return detailDto;
    }
}
