using Application.Features.Skills.Common.DTOs;
using MediatR;

namespace Application.Features.Skills.Public.GetByCategory.Queries;

public sealed record GetSkillsByCategoryQuery(Guid CategoryId) : IRequest<IReadOnlyList<SkillOptionDto>>;
