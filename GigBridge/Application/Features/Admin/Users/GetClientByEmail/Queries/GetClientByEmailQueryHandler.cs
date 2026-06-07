using Application.Common.Interfaces;
using Application.Features.Admin.Users.Shared.DTOs;
using AutoMapper;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.Users.GetClientByEmail.Queries;

public class GetClientByEmailQueryHandler : IRequestHandler<GetClientByEmailQuery, AdminUserDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;

    public GetClientByEmailQueryHandler(IApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<AdminUserDto?> Handle(GetClientByEmailQuery request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        var user = await _context.Set<User>()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email && u.Role == 0, cancellationToken);

        return user is null ? null : _mapper.Map<AdminUserDto>(user);
    }
}
