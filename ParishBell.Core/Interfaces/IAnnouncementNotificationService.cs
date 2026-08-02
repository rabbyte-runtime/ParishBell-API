using ParishBell.Core.DTOs.Notifications;

namespace ParishBell.Core.Interfaces;

public interface IAnnouncementNotificationService
{
    // NOTE: Enqueues push notifications for newly-published announcements, then delivers pending ones.
    Task<AnnouncementPushResult> ProcessPendingAsync(CancellationToken ct = default);
}
