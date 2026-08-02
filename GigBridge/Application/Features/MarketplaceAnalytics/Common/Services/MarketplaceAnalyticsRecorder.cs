using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Application.Features.MarketplaceAnalytics.Common.Services;

public interface IMarketplaceAnalyticsRecorder
{
    Task<Guid?> RecordSearchAsync(string actorIdentity, string? query, int resultCount, object filters, CancellationToken cancellationToken);
    Task RecordJobOpenAsync(string actorIdentity, Guid eventId, Guid jobPostId, Guid? searchEventId, CancellationToken cancellationToken);
    Task RecordJobSaveAsync(Guid userId, Guid jobPostId, DateTime occurredAt, CancellationToken cancellationToken);
}

public sealed class MarketplaceAnalyticsRecorder : IMarketplaceAnalyticsRecorder
{
    private static readonly Regex Email = new(@"\b[^\s@]+@[^\s@]+\.[^\s@]+\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Phone = new(@"(?:\+?\d[\s().-]*){8,}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Url = new(@"(?:https?://|www\.|\b[a-z0-9-]+\.(?:com|net|org|vn)\b)", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _clock;
    private readonly byte[] _hashKey;

    public MarketplaceAnalyticsRecorder(IApplicationDbContext context, IDateTimeService clock, IConfiguration configuration)
    {
        _context = context;
        _clock = clock;
        var key = configuration["Analytics:HashKey"] ?? configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Analytics:HashKey must be configured.");
        _hashKey = Encoding.UTF8.GetBytes(key);
    }

    public async Task<Guid?> RecordSearchAsync(
        string actorIdentity,
        string? query,
        int resultCount,
        object filters,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeSearch(query);
        if (normalized is null || string.IsNullOrWhiteSpace(actorIdentity)) return null;
        var now = _clock.UtcNow;
        var actor = Hmac(actorIdentity);
        var filterJson = JsonSerializer.Serialize(filters);
        var timeSlice = now.Ticks / TimeSpan.FromSeconds(10).Ticks;
        var dedupe = Digest($"search|{actor}|{normalized}|{filterJson}|{timeSlice}");
        if (await _context.Set<MarketplaceAnalyticsEvent>().AsNoTracking().AnyAsync(x => x.DedupeKey == dedupe, cancellationToken))
            return null;
        var id = Guid.NewGuid();
        _context.Set<MarketplaceAnalyticsEvent>().Add(new MarketplaceAnalyticsEvent
        {
            MarketplaceAnalyticsEventId = id,
            Type = MarketplaceAnalyticsEventType.Search,
            ActorKey = actor,
            DedupeKey = dedupe,
            NormalizedQuery = normalized,
            ResultCount = Math.Max(0, resultCount),
            FilterMetadata = filterJson,
            OccurredAt = now,
            CreatedAt = now
        });
        await _context.SaveChangesAsync(cancellationToken);
        return id;
    }

    public async Task RecordJobOpenAsync(
        string actorIdentity,
        Guid eventId,
        Guid jobPostId,
        Guid? searchEventId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(actorIdentity)) return;
        var now = _clock.UtcNow;
        var actor = Hmac(actorIdentity);
        var timeSlice = now.Ticks / TimeSpan.FromMinutes(5).Ticks;
        var dedupe = Digest($"open|{actor}|{jobPostId:N}|{searchEventId:N}|{timeSlice}");
        if (await _context.Set<MarketplaceAnalyticsEvent>().AsNoTracking()
            .AnyAsync(x => x.MarketplaceAnalyticsEventId == eventId || x.DedupeKey == dedupe, cancellationToken)) return;
        _context.Set<MarketplaceAnalyticsEvent>().Add(new MarketplaceAnalyticsEvent
        {
            MarketplaceAnalyticsEventId = eventId,
            Type = MarketplaceAnalyticsEventType.JobOpen,
            ActorKey = actor,
            DedupeKey = dedupe,
            JobPostId = jobPostId,
            SearchEventId = searchEventId,
            OccurredAt = now,
            CreatedAt = now
        });
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordJobSaveAsync(Guid userId, Guid jobPostId, DateTime occurredAt, CancellationToken cancellationToken)
    {
        var actor = Hmac($"user:{userId:N}");
        var dedupe = Digest($"save|{actor}|{jobPostId:N}");
        if (await _context.Set<MarketplaceAnalyticsEvent>().AsNoTracking().AnyAsync(x => x.DedupeKey == dedupe, cancellationToken)) return;
        _context.Set<MarketplaceAnalyticsEvent>().Add(new MarketplaceAnalyticsEvent
        {
            MarketplaceAnalyticsEventId = Guid.NewGuid(),
            Type = MarketplaceAnalyticsEventType.JobSave,
            ActorKey = actor,
            DedupeKey = dedupe,
            JobPostId = jobPostId,
            OccurredAt = occurredAt,
            CreatedAt = _clock.UtcNow
        });
        await _context.SaveChangesAsync(cancellationToken);
    }

    public static string? NormalizeSearch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = string.Join(' ', value.Normalize(NormalizationForm.FormKC)
            .Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
        if (normalized.Length is < 2 or > 120 || normalized.Any(char.IsControl) ||
            Email.IsMatch(normalized) || Phone.IsMatch(normalized) || Url.IsMatch(normalized)) return null;
        return normalized;
    }

    private string Hmac(string value)
    {
        using var hmac = new HMACSHA256(_hashKey);
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private static string Digest(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
