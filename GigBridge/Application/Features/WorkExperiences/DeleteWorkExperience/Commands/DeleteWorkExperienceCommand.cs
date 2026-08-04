using MediatR;

namespace Application.Features.WorkExperiences.DeleteWorkExperience.Commands;

public sealed record DeleteWorkExperienceCommand(Guid UserId, Guid WorkExperienceId) : IRequest<bool>;
