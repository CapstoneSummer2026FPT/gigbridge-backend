using Application.Common.Interfaces;
using Application.Features.Admin.Users.Shared.DTOs;
using MediatR;

namespace Application.Features.Admin.Users.GetFreelancerByEmail.Queries;

public record GetFreelancerByEmailQuery(string Email) : IRequest<AdminUserDto?>;
