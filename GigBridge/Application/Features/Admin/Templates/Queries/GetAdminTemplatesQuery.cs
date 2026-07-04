using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.Templates.Queries;

public sealed record EsignTemplateDto(
    Guid EsignTemplatesId,
    string Name,
    string TemplateCode,
    string HtmlContent,
    int Version,
    string? PlaceholderSchema,
    string? Description,
    bool IsActive,
    Guid CreatedBy,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record GetAdminTemplatesQuery(Guid AdminUserId) : IRequest<IReadOnlyList<EsignTemplateDto>>;

public sealed class GetAdminTemplatesQueryHandler :
    IRequestHandler<GetAdminTemplatesQuery, IReadOnlyList<EsignTemplateDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAdminTemplatesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<EsignTemplateDto>> Handle(
        GetAdminTemplatesQuery request,
        CancellationToken cancellationToken)
    {
        var admin = await _context.Set<User>()
            .FirstOrDefaultAsync(user => user.UserId == request.AdminUserId, cancellationToken);

        if (admin is null || admin.Role != (int)UserRole.Admin)
        {
            throw new ForbiddenAccessException("Only admins can manage contract templates.");
        }

        var templates = await _context.Set<EsignTemplate>()
            .AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        return templates.Select(t => new EsignTemplateDto(
            t.EsignTemplatesId,
            t.Name,
            t.TemplateCode,
            t.HtmlContent,
            t.Version,
            t.PlaceholderSchema,
            t.Description,
            t.IsActive,
            t.CreatedBy,
            t.CreatedAt,
            t.UpdatedAt)).ToList();
    }
}
