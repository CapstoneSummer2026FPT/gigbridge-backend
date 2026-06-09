using Application.Common.Interfaces;
using MediatR;

namespace Application.Features.Profiles.Common.MarkSetupComplete.Commands;

public record MarkSetupCompleteCommand : IRequest<bool>;
