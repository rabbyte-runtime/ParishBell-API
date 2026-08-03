using ParishBell.Core.DTOs.Notifications;
using ParishBell.Core.Entities;

namespace ParishBell.Core.Interfaces;

// NOTE: The sending side of parish feast days.
// NOTE: The user-facing inbox over the same table lives elsewhere.
public interface IFeastDayNotificationRepository
{
    // NOTE: Every feast pinned by a live church, with dates left unresolved.
    // NOTE: The service works out which of them land today.
    Task<List<DueFeastDay>> GetPinnedFeastDaysAsync(CancellationToken ct = default);

    // NOTE: Followers who want feast pushes and have no log row for this occurrence.
    Task<List<FeastDayRecipient>> GetRecipientsWithoutLogAsync(Guid locationFeastDayId, Guid locationId, DateOnly occurrenceDate, CancellationToken ct = default);

    // NOTE: Title and description per language, so each push uses the right one.
    Task<Dictionary<Guid, (string Title, string? Description)>> GetTranslationsAsync(Guid calendarId, CancellationToken ct = default);

    // NOTE: Queues the rows unsent, so a crash leaves them for the next poll.
    Task AddLogsAsync(IReadOnlyCollection<NotificationsLog> logs, CancellationToken ct = default);

    // NOTE: Queued feast-day rows awaiting delivery, oldest first.
    Task<List<PendingNotification>> GetPendingAsync(int batchSize, CancellationToken ct = default);

    // NOTE: Marks the given rows delivered.
    Task MarkSentAsync(IReadOnlyCollection<Guid> notificationIds, DateTime sentAt, CancellationToken ct = default);
}
