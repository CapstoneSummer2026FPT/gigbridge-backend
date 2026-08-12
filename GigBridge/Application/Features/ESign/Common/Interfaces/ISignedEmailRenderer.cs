using Application.Features.Contracts.Signing.Common.Sign.DTOs;

namespace Application.Features.ESign.Common.Interfaces;

public interface ISignedEmailRenderer
{
    RenderedSignedEmail Render(SignedEmailModel model);
}
