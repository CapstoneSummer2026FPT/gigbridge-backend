namespace Application.Common.Interfaces.IService;

public interface IDeadlineWarningService
{
    Task CheckDeadlinesAsync(CancellationToken cancellationToken);
}
