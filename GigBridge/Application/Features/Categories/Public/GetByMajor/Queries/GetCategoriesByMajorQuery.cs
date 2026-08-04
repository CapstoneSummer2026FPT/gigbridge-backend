using Application.Features.Categories.Common.DTOs;
using MediatR;

namespace Application.Features.Categories.Public.GetByMajor.Queries;

public sealed record GetCategoriesByMajorQuery(Guid MajorId) : IRequest<IReadOnlyList<CategoryOptionDto>>;
