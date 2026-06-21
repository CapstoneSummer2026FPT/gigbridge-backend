using Application.Features.MajorCategories.Common.DTOs;
using MediatR;

namespace Application.Features.MajorCategories.Public.GetAll.Queries;

public sealed record GetAllMajorCategoriesQuery : IRequest<IReadOnlyList<MajorCategoryDto>>;
