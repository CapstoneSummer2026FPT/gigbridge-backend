using Application.Common.Interfaces;
using MediatR;

namespace Application.Features.Profiles.MarkSetupComplete.Commands;

public record MarkSetupCompleteCommand : IRequest<bool>, IRequireAuthentication;
