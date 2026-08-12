using System.Globalization;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Features.Admin.AuditLogs.Common.Interfaces;
using Application.Features.Admin.Elo.Common;
using Application.Features.Elo.Common;
using Application.Features.Elo.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.Elo.Commands.UpdateEloPolicy;

public sealed class UpdateEloPolicyCommandHandler : IRequestHandler<UpdateEloPolicyCommand, EloPolicyDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _clock;
    private readonly IAdminAuditService _audit;

    public UpdateEloPolicyCommandHandler(
        IApplicationDbContext context,
        IDateTimeService clock,
        IAdminAuditService audit)
    {
        _context = context;
        _clock = clock;
        _audit = audit;
    }

    public async Task<EloPolicyDto> Handle(UpdateEloPolicyCommand command, CancellationToken cancellationToken)
    {
        await AdminEloSupport.EnsureAdminAsync(_context, command.AdminId, cancellationToken);
        EloPolicy.Validate(command.Mode, command.Value);

        var before = await EloPolicy.LoadAsync(_context, cancellationToken);
        var settings = await _context.Set<PlatformSetting>()
            .Where(x => x.Key == EloPolicy.DisputePenaltyModeKey ||
                        x.Key == EloPolicy.DisputePenaltyValueKey)
            .ToListAsync(cancellationToken);

        var now = _clock.UtcNow;
        Upsert(settings, EloPolicy.DisputePenaltyModeKey, command.Mode.ToString(), command.AdminId, now);
        Upsert(settings, EloPolicy.DisputePenaltyValueKey,
            command.Value.ToString(CultureInfo.InvariantCulture), command.AdminId, now);

        foreach (var setting in settings)
        {
            if (setting.PlatformSettingsId == Guid.Empty)
            {
                setting.PlatformSettingsId = Guid.NewGuid();
                _context.Set<PlatformSetting>().Add(setting);
            }
        }

        _audit.Add(command.AdminId, "Elo.PolicyUpdate", nameof(PlatformSetting), null,
            before, new EloPolicyDto(command.Mode, command.Value));

        await _context.SaveChangesAsync(cancellationToken);
        return new EloPolicyDto(command.Mode, command.Value);
    }

    private static void Upsert(
        ICollection<PlatformSetting> settings,
        string key,
        string value,
        Guid adminUserId,
        DateTime now)
    {
        var setting = settings.FirstOrDefault(x => x.Key == key);
        if (setting is null)
        {
            settings.Add(new PlatformSetting
            {
                Key = key,
                Value = value,
                DataType = "string",
                Description = "Elo dispute-resolution penalty policy",
                UpdatedAt = now,
                UpdatedByAdminId = adminUserId
            });
            return;
        }

        setting.Value = value;
        setting.UpdatedAt = now;
        setting.UpdatedByAdminId = adminUserId;
    }
}
