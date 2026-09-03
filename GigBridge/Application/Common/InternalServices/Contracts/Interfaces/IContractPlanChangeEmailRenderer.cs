using Application.Common.InternalServices.Contracts.Models;

namespace Application.Common.InternalServices.Contracts.Interfaces;

public interface IContractPlanChangeEmailRenderer
{
    RenderedContractPlanChangeEmail Render(ContractPlanChangeEmailModel model);
}
