using ParishBell.Core.DTOs.Common;

namespace ParishBell.Core.Interfaces;

// NOTE: The user-facing inbox over notifications_log.
// NOTE: The sender side lives in the per-type notification repositories.
public interface IUserNotificationRepository
{
    // NOTE: Delivered notifications, newest first. Only the four user-facing types.
    Task<IReadOnlyList<NotificationResult>> GetForUserAsync(Guid userId, int skip, int take, CancellationToken ct = default);

    // NOTE: How many of those rows are unread, counting only what the list returns.
    // NOTE: The badge therefore cannot outrun the list.
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default);

    // NOTE: Marks one read. False when missing or not theirs. Idempotent.
    Task<bool> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct = default);

    // NOTE: Marks every unread notification for the user as read. Returns how many were still unread.
    Task<int> MarkAllReadAsync(Guid userId, CancellationToken ct = default);
}
