using Application.Features.Auth.SendOtp.DTOs;
using MediatR;

namespace Application.Features.Auth.SendOtp.Commands
{
    public class SendOtpCommand : IRequest<Unit>
    {
        public SendOtpRequest SendOtpRequest { get; }

        public SendOtpCommand(SendOtpRequest request)
        {
            SendOtpRequest = request;
        }
    }
}
