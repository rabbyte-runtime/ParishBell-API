using ParishBell.Core.DTOs.Notifications;
using ParishBell.Core.Entities;

namespace ParishBell.Core.Interfaces;

// NOTE: The sending side of parish feast days. The user-facing inbox over the same table lives in IUserNotificationRepository.
public interface IFeastDayNotificationRepository
{
    // NOTE: Every feast pinned by a live church. Dates are returned unresolved - the service works out which land today.
    Task<List<DueFeastDay>> GetPinnedFeastDaysAsync(CancellationToken ct = default);

    // NOTE: Followers of that church who still want feast pushes and have no log row for this occurrence yet.
    Task<List<FeastDayRecipient>> GetRecipientsWithoutLogAsync(Guid locationFeastDayId, Guid locationId, DateOnly occurrenceDate, CancellationToken ct = default);

    // NOTE: The feast's title and description per language, so each recipient's push is written in their own.
    Task<Dictionary<Guid, (string Title, string? Description)>> GetTranslationsAsync(Guid calendarId, CancellationToken ct = default);

    // NOTE: Queues the rows unsent, so a crash before delivery leaves them for the next poll rather than losing them.
    Task AddLogsAsync(IReadOnlyCollection<NotificationsLog> logs, CancellationToken ct = default);

    // NOTE: Queued feast-day rows awaiting delivery, oldest first.
    Task<List<PendingNotification>> GetPendingAsync(int batchSize, CancellationToken ct = default);

    // NOTE: Marks the given rows delivered.
    Task MarkSentAsync(IReadOnlyCollection<Guid> notificationIds, DateTime sentAt, CancellationToken ct = default);
}
