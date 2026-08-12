namespace Application.Features.Contracts.Common.Email;

public interface ISignedEmailRenderer
{
    RenderedSignedEmail Render(SignedEmailModel model);
}
