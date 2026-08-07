using Domain.Enums;

namespace Application.Features.Elo.DTOs;

/// <summary>
/// Platform-configured Elo policy. Currently governs the dispute-resolution
/// penalty amount and whether it is expressed as a percentage of current points
/// or as fixed points. Stored in PlatformSetting rows and editable by admins.
/// </summary>
public sealed record EloPolicyDto(EloAdjustmentMode Mode, decimal Value);
