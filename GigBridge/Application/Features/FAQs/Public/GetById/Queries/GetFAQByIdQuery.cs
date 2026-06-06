using Application.Features.FAQs.Shared.DTOs;
using MediatR;

namespace Application.Features.FAQs.GetById.Queries;

public sealed record GetFAQByIdQuery(int Id) : IRequest<FAQDto?>;
