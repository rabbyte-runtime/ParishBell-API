using ParishBell.Core.DTOs.Notifications;
using ParishBell.Core.Entities;

namespace ParishBell.Core.Interfaces;

// NOTE: The sending side of mass reminders.
// NOTE: The user-facing inbox over the same table lives elsewhere.
public interface IMassReminderNotificationRepository
{
    // NOTE: Every switched-on reminder, for a live mass at a live church.
    // NOTE: Only for owners who still want mass pushes.
    // NOTE: Returns candidates by weekday, not by time - the service decides which have actually come due.
    Task<List<DueMassReminder>> GetActiveRemindersAsync(IReadOnlyCollection<int> daysOfWeek, CancellationToken ct = default);

    // NOTE: The (user, schedule, date) triples already logged in the window.
    // NOTE: Stops a poll re-sending what an earlier one already did.
    Task<HashSet<(Guid UserId, Guid ScheduleId, DateOnly OccurrenceDate)>> GetAlreadyNotifiedAsync(
        DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);

    // NOTE: Queues the rows unsent, so a crash leaves them for the next poll.
    Task AddLogsAsync(IReadOnlyCollection<NotificationsLog> logs, CancellationToken ct = default);

    // NOTE: Queued mass-reminder rows awaiting delivery, oldest first.
    Task<List<PendingNotification>> GetPendingAsync(int batchSize, CancellationToken ct = default);

    // NOTE: Marks the given rows delivered.
    Task MarkSentAsync(IReadOnlyCollection<Guid> notificationIds, DateTime sentAt, CancellationToken ct = default);
}
