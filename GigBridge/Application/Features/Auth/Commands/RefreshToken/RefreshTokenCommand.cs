using Application.Common.Interfaces.IService;
using Application.Features.Auth.DTOs;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.Auth.Commands.RefreshToken;

public record RefreshTokenCommand(string AccessToken, string RefreshToken) : IRequest<(LoginResponse LoginData, string RefreshToken)>;


