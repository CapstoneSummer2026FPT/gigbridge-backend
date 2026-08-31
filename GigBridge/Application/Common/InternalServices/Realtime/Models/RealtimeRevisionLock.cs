using System.Buffers.Binary;

namespace Application.Common.InternalServices.Realtime.Models;

/// <summary>
/// Produces stable PostgreSQL advisory-lock keys for realtime revision resources.
/// User state uses one shared key across notification and conversation revisions so
/// concurrent first writes cannot both try to create the same UserRealtimeState row.
/// </summary>
public static class RealtimeRevisionLock
{
    private const long UserDiscriminator = 0x52544D5553455201;
    private const long ConversationDiscriminator = 0x52544D434F4E5602;
    private const long ReceiptDiscriminator = 0x52544D5243505403;

    public static long ForUser(Guid userId) => ForResource(userId, UserDiscriminator);

    public static long ForConversation(Guid conversationId) =>
        ForResource(conversationId, ConversationDiscriminator);

    public static long ForReceipt(Guid receiptId) => ForResource(receiptId, ReceiptDiscriminator);

    public static long[] OrderDistinct(IEnumerable<long> lockKeys) =>
        lockKeys.Distinct().Order().ToArray();

    private static long ForResource(Guid id, long discriminator)
    {
        Span<byte> bytes = stackalloc byte[16];
        id.TryWriteBytes(bytes);
        return BinaryPrimitives.ReadInt64LittleEndian(bytes[..8]) ^
            BinaryPrimitives.ReadInt64LittleEndian(bytes[8..]) ^
            discriminator;
    }
}
