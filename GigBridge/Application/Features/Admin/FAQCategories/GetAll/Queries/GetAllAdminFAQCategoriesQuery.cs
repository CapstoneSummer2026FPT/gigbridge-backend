using Application.Features.FAQCategories.Shared.DTOs;
using MediatR;

namespace Application.Features.Admin.FAQCategories.GetAll.Queries;

public sealed record GetAllAdminFAQCategoriesQuery : IRequest<IReadOnlyList<FAQCategoryDto>>;
