using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums.Accounts;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Application.Features.Admin.Templates.Commands;

public sealed record CreateAdminTemplateCommand(
    Guid AdminUserId,
    string Name,
    string TemplateCode,
    string HtmlContent,
    int Version,
    string? PlaceholderSchema,
    string? Description,
    bool IsActive) : IRequest<Guid>;

public sealed class CreateAdminTemplateCommandHandler :
    IRequestHandler<CreateAdminTemplateCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateAdminTemplateCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(
        CreateAdminTemplateCommand request,
        CancellationToken cancellationToken)
    {
        var admin = await _context.Set<User>()
            .FirstOrDefaultAsync(user => user.UserId == request.AdminUserId, cancellationToken);

        if (admin is null || admin.Role != (int)UserRole.Admin)
        {
            throw new ForbiddenAccessException("Only admins can manage contract templates.");
        }

        var template = new EsignTemplate
        {
            EsignTemplatesId = Guid.NewGuid(),
            Name = request.Name,
            TemplateCode = request.TemplateCode,
            HtmlContent = request.HtmlContent,
            Version = request.Version,
            PlaceholderSchema = request.PlaceholderSchema,
            Description = request.Description,
            IsActive = request.IsActive,
            CreatedBy = request.AdminUserId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Set<EsignTemplate>().Add(template);

        var auditLog = new AdminAuditLog
        {
            AdminAuditLogsId = Guid.NewGuid(),
            AdminId = request.AdminUserId,
            Action = "CreateEsignTemplate",
            EntityId = template.EsignTemplatesId,
            EntityType = "EsignTemplate",
            NewValues = JsonSerializer.Serialize(new
            {
                template.Name,
                template.TemplateCode,
                template.HtmlContent,
                template.Version,
                template.PlaceholderSchema,
                template.Description,
                template.IsActive
            }),
            CreatedAt = DateTime.UtcNow
        };

        _context.Set<AdminAuditLog>().Add(auditLog);

        await _context.SaveChangesAsync(cancellationToken);

        return template.EsignTemplatesId;
    }
}
