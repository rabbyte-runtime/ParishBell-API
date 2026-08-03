using ParishBell.Core.DTOs.Notifications;
using ParishBell.Core.Entities;

namespace ParishBell.Core.Interfaces;

public interface IAnnouncementNotificationRepository
{
    // NOTE: Active, unexpired announcements created since the cutoff, with their per-language text.
    Task<IReadOnlyList<PendingAnnouncement>> GetAnnouncementsToNotifyAsync(DateTime createdAfterUtc, DateTime nowUtc, CancellationToken ct = default);

    // NOTE: Followers with no notifications_log row for this announcement yet.
    Task<IReadOnlyList<NotificationRecipient>> GetFollowersWithoutLogAsync(Guid announcementId, Guid locationId, CancellationToken ct = default);

    // NOTE: Bulk-inserts queued (is_sent=false) notification rows.
    Task AddLogsAsync(IReadOnlyCollection<NotificationsLog> logs, CancellationToken ct = default);

    // NOTE: Up to max undelivered rows of the given type.
    Task<IReadOnlyList<PendingNotification>> GetPendingAsync(short type, int max, CancellationToken ct = default);

    // NOTE: Marks the given rows delivered (is_sent=true, sent_at=now).
    Task MarkSentAsync(IReadOnlyCollection<Guid> notificationIds, DateTime sentAtUtc, CancellationToken ct = default);
}
