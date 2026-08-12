using Application.Features.Contracts.Signing.Common.Sign.DTOs;

namespace Application.Common.Interfaces.IService;

public interface ISignedEmailRenderer
{
    RenderedSignedEmail Render(SignedEmailModel model);
}
