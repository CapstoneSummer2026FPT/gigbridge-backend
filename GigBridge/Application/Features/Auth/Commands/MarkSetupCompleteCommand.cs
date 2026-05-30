using Application.Features.Auth.Shared.DTOs;
using MediatR;

namespace Application.Features.Auth.Commands
{
    public class MarkSetupCompleteCommand : IRequest<UserDTO>
    {
        public Guid UserId { get; set; }

        public MarkSetupCompleteCommand(Guid userId)
        {
            UserId = userId;
        }
    }
}
