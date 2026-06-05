using Application.Features.FAQCategories.Shared.DTOs;
using MediatR;

namespace Application.Features.FAQCategories.GetAll.Queries;

public sealed record GetAllFAQCategoriesQuery : IRequest<IReadOnlyList<FAQCategoryDto>>;
