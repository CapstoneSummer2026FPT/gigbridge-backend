using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Application.Features.Admin.Templates.Commands;

public sealed record DeleteAdminTemplateCommand(
    Guid AdminUserId,
    Guid TemplateId) : IRequest<bool>;

public sealed class DeleteAdminTemplateCommandHandler :
    IRequestHandler<DeleteAdminTemplateCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public DeleteAdminTemplateCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(
        DeleteAdminTemplateCommand request,
        CancellationToken cancellationToken)
    {
        var admin = await _context.Set<User>()
            .FirstOrDefaultAsync(user => user.UserId == request.AdminUserId, cancellationToken);

        if (admin is null || admin.Role != (int)UserRole.Admin)
        {
            throw new ForbiddenAccessException("Only admins can manage contract templates.");
        }

        var template = await _context.Set<EsignTemplate>()
            .Include(t => t.EsignDocuments)
            .FirstOrDefaultAsync(x => x.EsignTemplatesId == request.TemplateId, cancellationToken);

        if (template is null)
        {
            throw new NotFoundException("Contract template does not exist.");
        }

        if (template.EsignDocuments.Any())
        {
            throw new BadRequestException("Cannot delete template as it is referenced by existing contract documents. Please deactivate it instead.");
        }

        var oldValues = JsonSerializer.Serialize(new
        {
            template.Name,
            template.TemplateCode,
            template.HtmlContent,
            template.Version,
            template.PlaceholderSchema,
            template.Description,
            template.IsActive
        });

        _context.Set<EsignTemplate>().Remove(template);

        var auditLog = new AdminAuditLog
        {
            AdminAuditLogsId = Guid.NewGuid(),
            AdminId = request.AdminUserId,
            Action = "DeleteEsignTemplate",
            EntityId = template.EsignTemplatesId,
            EntityType = "EsignTemplate",
            OldValues = oldValues,
            CreatedAt = DateTime.UtcNow
        };

        _context.Set<AdminAuditLog>().Add(auditLog);

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
