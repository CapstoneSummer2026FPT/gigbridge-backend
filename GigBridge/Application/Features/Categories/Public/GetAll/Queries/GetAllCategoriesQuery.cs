using Application.Features.Categories.Common.DTOs;
using MediatR;

namespace Application.Features.Categories.Public.GetAll.Queries;

public sealed record GetAllCategoriesQuery : IRequest<IReadOnlyList<CategoryDto>>;
