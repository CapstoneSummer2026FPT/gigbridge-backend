using Application.Features.FAQs.Shared.DTOs;
using Application.Features.Admin.FAQs.Update.DTOs;
using MediatR;

namespace Application.Features.Admin.FAQs.Update.Commands;

public sealed record UpdateFAQCommand(int Id, UpdateFAQRequest Request) : IRequest<FAQDto?>;
