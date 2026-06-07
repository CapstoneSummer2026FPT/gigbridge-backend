using MediatR;

namespace Application.Features.Admin.FAQCategories.Delete.Commands;

public sealed record DeleteFAQCategoryCommand(int Id) : IRequest<bool>;
