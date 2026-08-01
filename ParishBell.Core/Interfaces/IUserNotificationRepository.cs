using ParishBell.Core.DTOs.Common;

namespace ParishBell.Core.Interfaces;

// NOTE: The user-facing inbox over notifications_log. The sender side lives in IAnnouncementNotificationRepository.
public interface IUserNotificationRepository
{
    // NOTE: Delivered notifications for the user, newest first. Only the four user-facing types are returned.
    Task<IReadOnlyList<NotificationResult>> GetForUserAsync(Guid userId, int skip, int take, CancellationToken ct = default);

    // NOTE: Marks one notification read. False when it does not exist or belongs to someone else. Idempotent.
    Task<bool> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct = default);

    // NOTE: Marks every unread notification for the user as read. Returns how many were still unread.
    Task<int> MarkAllReadAsync(Guid userId, CancellationToken ct = default);
}
