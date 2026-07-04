using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Application.Features.Admin.Templates.Commands;

public sealed record UpdateAdminTemplateCommand(
    Guid AdminUserId,
    Guid TemplateId,
    string Name,
    string TemplateCode,
    string HtmlContent,
    int Version,
    string? PlaceholderSchema,
    string? Description,
    bool IsActive) : IRequest<bool>;

public sealed class UpdateAdminTemplateCommandHandler :
    IRequestHandler<UpdateAdminTemplateCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public UpdateAdminTemplateCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(
        UpdateAdminTemplateCommand request,
        CancellationToken cancellationToken)
    {
        var admin = await _context.Set<User>()
            .FirstOrDefaultAsync(user => user.UserId == request.AdminUserId, cancellationToken);

        if (admin is null || admin.Role != (int)UserRole.Admin)
        {
            throw new ForbiddenAccessException("Only admins can manage contract templates.");
        }

        var template = await _context.Set<EsignTemplate>()
            .FirstOrDefaultAsync(x => x.EsignTemplatesId == request.TemplateId, cancellationToken);

        if (template is null)
        {
            throw new NotFoundException("Contract template does not exist.");
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

        template.Name = request.Name;
        template.TemplateCode = request.TemplateCode;
        template.HtmlContent = request.HtmlContent;
        template.Version = request.Version;
        template.PlaceholderSchema = request.PlaceholderSchema;
        template.Description = request.Description;
        template.IsActive = request.IsActive;
        template.UpdatedAt = DateTime.UtcNow;

        var newValues = JsonSerializer.Serialize(new
        {
            template.Name,
            template.TemplateCode,
            template.HtmlContent,
            template.Version,
            template.PlaceholderSchema,
            template.Description,
            template.IsActive
        });

        var auditLog = new AdminAuditLog
        {
            AdminAuditLogsId = Guid.NewGuid(),
            AdminId = request.AdminUserId,
            Action = "UpdateEsignTemplate",
            EntityId = template.EsignTemplatesId,
            EntityType = "EsignTemplate",
            OldValues = oldValues,
            NewValues = newValues,
            CreatedAt = DateTime.UtcNow
        };

        _context.Set<AdminAuditLog>().Add(auditLog);

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
