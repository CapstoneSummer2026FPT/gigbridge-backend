using Application.Common.Interfaces;
using Application.Features.Admin.Users.Shared.DTOs;
using AutoMapper;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.Users.GetFreelancerByEmail.Queries;

public class GetFreelancerByEmailQueryHandler : IRequestHandler<GetFreelancerByEmailQuery, AdminUserDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;

    public GetFreelancerByEmailQueryHandler(IApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<AdminUserDto?> Handle(GetFreelancerByEmailQuery request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        var user = await _context.Set<User>()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email && u.Role == 1, cancellationToken);

        return user is null ? null : _mapper.Map<AdminUserDto>(user);
    }
}
