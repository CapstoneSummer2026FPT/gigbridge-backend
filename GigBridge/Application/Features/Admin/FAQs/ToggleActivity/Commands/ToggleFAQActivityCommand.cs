using MediatR;

namespace Application.Features.Admin.FAQs.ToggleActivity.Commands;

public sealed record ToggleFAQActivityCommand(int Id) : IRequest<bool>;
