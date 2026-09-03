namespace Domain.Entities;

public sealed class UserRealtimeState
{
    public Guid UserId { get; set; }
    public int NotificationRevision { get; set; }
    public int NotificationUnreadCount { get; set; }
    public int ConversationRevision { get; set; }
    public int ConversationUnreadCount { get; set; }
    public DateTime UpdatedAt { get; set; }
    public User User { get; set; } = null!;
}
