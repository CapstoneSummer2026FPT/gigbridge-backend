using MediatR;

namespace Application.Features.Admin.FAQCategories.ToggleActivity.Commands;

public sealed record ToggleFAQCategoryActivityCommand(int Id) : IRequest<bool>;
