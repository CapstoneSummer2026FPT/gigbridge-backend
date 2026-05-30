using Application.Features.Auth.VerifyOtp.DTOs;
using MediatR;

namespace Application.Features.Auth.VerifyOtp.Commands
{
    public class VerifyOtpCommand : IRequest<Unit>
    {
        public VerifyOtpRequest VerifyOtpRequest { get; }

        public VerifyOtpCommand(VerifyOtpRequest request)
        {
            VerifyOtpRequest = request;
        }
    }
}
