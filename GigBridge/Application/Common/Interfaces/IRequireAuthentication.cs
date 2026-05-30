namespace Application.Common.Interfaces;

/// <summary>
/// Marker interface for MediatR requests that require an authenticated user.
/// Apply this to any Command/Query that should only be executed by logged-in users.
/// The AuthorizationBehaviour will automatically check authentication before the handler runs.
/// </summary>
public interface IRequireAuthentication
{
}
