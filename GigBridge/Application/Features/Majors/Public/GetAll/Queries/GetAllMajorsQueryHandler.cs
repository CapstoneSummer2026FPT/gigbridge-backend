using Application.Common.Interfaces;
using Application.Features.Majors.Common.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Majors.Public.GetAll.Queries;

public sealed class GetAllMajorsQueryHandler : IRequestHandler<GetAllMajorsQuery, IReadOnlyList<MajorDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAllMajorsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<MajorDto>> Handle(GetAllMajorsQuery request, CancellationToken cancellationToken)
    {
        return await _context.Set<Major>()
            .AsNoTracking()
            .Where(major => major.IsActive)
            .OrderBy(major => major.SortOrder)
            .ThenBy(major => major.Name)
            .Select(major => new MajorDto(
                major.MajorsId,
                major.Name,
                major.Slug,
                major.Description,
                major.SortOrder))
            .ToListAsync(cancellationToken);
    }
}
