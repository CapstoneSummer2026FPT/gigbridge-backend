using Application.Features.Auth.Dto;
using MediatR;
namespace Application.Features.Auth.Command;
public record LoginCommand(string Username, string Password) : IRequest<LoginResponse?>;
