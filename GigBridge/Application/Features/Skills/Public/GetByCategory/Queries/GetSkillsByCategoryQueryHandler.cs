using Application.Common.Interfaces;
using Application.Features.Skills.Common.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Skills.Public.GetByCategory.Queries;

public sealed class GetSkillsByCategoryQueryHandler
    : IRequestHandler<GetSkillsByCategoryQuery, IReadOnlyList<SkillOptionDto>>
{
    private readonly IApplicationDbContext _context;

    public GetSkillsByCategoryQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<SkillOptionDto>> Handle(
        GetSkillsByCategoryQuery request,
        CancellationToken cancellationToken)
    {
        return await _context.Set<CategorySkill>()
            .AsNoTracking()
            .Where(categorySkill =>
                categorySkill.CategoryId == request.CategoryId &&
                categorySkill.Category.IsActive &&
                categorySkill.Skill.IsActive)
            .OrderBy(categorySkill => categorySkill.Skill.Name)
            .Select(categorySkill => new SkillOptionDto(
                categorySkill.SkillId,
                categorySkill.Skill.Name))
            .ToListAsync(cancellationToken);
    }
}
