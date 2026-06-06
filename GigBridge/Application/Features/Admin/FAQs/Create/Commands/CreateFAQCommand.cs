using Application.Features.Admin.FAQs.Create.DTOs;
using Application.Features.FAQs.Shared.DTOs;
using MediatR;

namespace Application.Features.Admin.FAQs.Create.Commands;

public sealed record CreateFAQCommand(CreateFAQRequest Request) : IRequest<FAQDto>;
