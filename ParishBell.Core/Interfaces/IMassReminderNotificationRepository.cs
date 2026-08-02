using ParishBell.Core.DTOs.Notifications;
using ParishBell.Core.Entities;

namespace ParishBell.Core.Interfaces;

// NOTE: The sending side of mass reminders. The user-facing inbox over the same table lives in IUserNotificationRepository.
public interface IMassReminderNotificationRepository
{
    // NOTE: Every reminder that is switched on, for a live mass at a live church, whose owner still wants mass pushes.
    // NOTE: Returns candidates by weekday, not by time - the service decides which have actually come due.
    Task<List<DueMassReminder>> GetActiveRemindersAsync(IReadOnlyCollection<int> daysOfWeek, CancellationToken ct = default);

    // NOTE: The (user, schedule, date) triples already logged in the window, so a poll never re-sends what an earlier one did.
    Task<HashSet<(Guid UserId, Guid ScheduleId, DateOnly OccurrenceDate)>> GetAlreadyNotifiedAsync(
        DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);

    // NOTE: Queues the rows. They are written unsent so a crash before delivery leaves them for the next poll, not lost.
    Task AddLogsAsync(IReadOnlyCollection<NotificationsLog> logs, CancellationToken ct = default);

    // NOTE: Queued mass-reminder rows awaiting delivery, oldest first.
    Task<List<PendingNotification>> GetPendingAsync(int batchSize, CancellationToken ct = default);

    // NOTE: Marks the given rows delivered.
    Task MarkSentAsync(IReadOnlyCollection<Guid> notificationIds, DateTime sentAt, CancellationToken ct = default);
}
