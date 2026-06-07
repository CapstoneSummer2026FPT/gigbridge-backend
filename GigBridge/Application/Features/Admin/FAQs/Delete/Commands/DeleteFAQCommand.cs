using MediatR;

namespace Application.Features.Admin.FAQs.Delete.Commands;

public sealed record DeleteFAQCommand(int Id) : IRequest<bool>;
