using Application.Common.Interfaces;
using Application.Features.Auth.Commands;
using Application.Features.Auth.Shared.DTOs;
using AutoMapper;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Auth.Handlers
{
    public class MarkSetupCompleteCommandHandler : IRequestHandler<MarkSetupCompleteCommand, UserDTO>
    {
        private readonly IApplicationDbContext _context;
        private readonly IMapper _mapper;

        public MarkSetupCompleteCommandHandler(IApplicationDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<UserDTO> Handle(MarkSetupCompleteCommand request, CancellationToken cancellationToken)
        {
            // Get user from database
            var user = await _context.Set<User>()
                .FirstOrDefaultAsync(u => u.UserId == request.UserId, cancellationToken);

            if (user == null)
                throw new KeyNotFoundException($"User with ID {request.UserId} not found");

            // Update IsSetup flag
            user.IsSetup = true;
            user.UpdatedAt = DateTime.UtcNow;

            _context.Set<User>().Update(user);
            await _context.SaveChangesAsync(cancellationToken);

            return _mapper.Map<UserDTO>(user);
        }
    }
}
