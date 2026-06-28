using Application.Features.Admin.Cheating.DTOs;
using MediatR;

namespace Application.Features.Admin.Cheating.ReviewViolation.Commands;

public record ReviewCheatingViolationCommand(
    Guid ViolationId,
    Guid AdminUserId,
    ReviewCheatingViolationRequest Request) : IRequest<AdminCheatingViolationDto>;
