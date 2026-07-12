using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.Templates.Queries;

public sealed record GetAdminTemplateByIdQuery(
    Guid AdminUserId,
    Guid TemplateId) : IRequest<EsignTemplateDto>;

public sealed class GetAdminTemplateByIdQueryHandler :
    IRequestHandler<GetAdminTemplateByIdQuery, EsignTemplateDto>
{
    private readonly IApplicationDbContext _context;

    public GetAdminTemplateByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<EsignTemplateDto> Handle(
        GetAdminTemplateByIdQuery request,
        CancellationToken cancellationToken)
    {
        var admin = await _context.Set<User>()
            .FirstOrDefaultAsync(user => user.UserId == request.AdminUserId, cancellationToken);

        if (admin is null || admin.Role != (int)UserRole.Admin)
        {
            throw new ForbiddenAccessException("Only admins can manage contract templates.");
        }

        var t = await _context.Set<EsignTemplate>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.EsignTemplatesId == request.TemplateId, cancellationToken);

        if (t is null)
        {
            throw new NotFoundException("Contract template does not exist.");
        }

        return new EsignTemplateDto(
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
            t.UpdatedAt);
    }
}
