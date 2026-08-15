using Application.Common.InternalServices.ESign.Models;
using Application.Features.Contracts.Signing.Common.Sign.DTOs;

namespace Application.Common.InternalServices.ESign.Interfaces;
public interface ISignedEmailRenderer
{
    RenderedSignedEmail Render(SignedEmailModel model);
}
