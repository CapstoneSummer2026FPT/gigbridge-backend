using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Application.Features.Premium.Common;

internal readonly record struct PromotionInteractionIdentity(string Key, long LockKey);

internal static class PromotionInteractionIdentityFactory
{
    internal static PromotionInteractionIdentity Create(
        string scope,
        Guid promotionId,
        string interactionType,
        string visitorKey,
        DateTime utcNow,
        int deduplicationSeconds)
    {
        var seconds = Math.Max(1, deduplicationSeconds);
        var utc = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        var bucket = new DateTimeOffset(utc).ToUnixTimeSeconds() / seconds;
        var visitorHash = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(visitorKey.Trim())));
        var key = $"promotion-interaction:{scope}:{promotionId:N}:{interactionType}:{visitorHash}:{bucket}";
        var lockKey = BinaryPrimitives.ReadInt64BigEndian(
            SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        return new PromotionInteractionIdentity(key, lockKey);
    }
}
