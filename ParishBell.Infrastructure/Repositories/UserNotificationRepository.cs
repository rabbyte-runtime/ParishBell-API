using Microsoft.EntityFrameworkCore;
using ParishBell.Core.DTOs.Common;
using ParishBell.Core.Entities;
using ParishBell.Core.Enums;
using ParishBell.Core.Interfaces;
using ParishBell.Infrastructure.Data;

namespace ParishBell.Infrastructure.Repositories;

public class UserNotificationRepository(ParishBellDbContext dbContext) : IUserNotificationRepository
{
    private readonly ParishBellDbContext _dbContext = dbContext;

    private const short EventType = (short)NotificationType.Event;
    private const short AnnouncementType = (short)NotificationType.Announcement;
    private const short MassReminderType = (short)NotificationType.MassReminder;
    private const short FeastDayType = (short)NotificationType.FeastDay;

    // NOTE: The one definition of "in the user's inbox" - shared so the list and the unread badge can never disagree about what counts.
    private IQueryable<NotificationsLog> UserInbox(Guid userId) =>
        _dbContext.NotificationsLogs
            .AsNoTracking()
            // IMPORTANT: Only delivered rows belong in the inbox - unsent ones are outbox entries the user never received.
            .Where(n => n.UserId == userId && n.IsSent && n.SentAt != null)
            // NOTE: Type 5 (System) has no deep-link target and is not part of the client's type union.
            .Where(n => n.Type == EventType || n.Type == AnnouncementType || n.Type == MassReminderType || n.Type == FeastDayType);

    public async Task<IReadOnlyList<NotificationResult>> GetForUserAsync(Guid userId, int skip, int take, CancellationToken ct = default)
    {
        return await UserInbox(userId)
            .OrderByDescending(n => n.SentAt)
            .ThenByDescending(n => n.NotificationId)
            .Skip(skip)
            .Take(take)
            .Select(n => new NotificationResult
            {
                NotificationId = n.NotificationId,
                Type = n.Type,
                Title = n.Title,
                Body = n.Body,
                SentAt = n.SentAt!.Value,
                IsRead = n.IsRead,
                ReferenceId = n.ReferenceId,
                OccurrenceDate = n.OccurrenceDate,

                // NOTE: reference_id is polymorphic, so the owning church is looked up in whichever table the type points at.
                LocationId =
                    n.Type == EventType
                        ? _dbContext.Events.Where(e => e.EventId == n.ReferenceId).Select(e => (Guid?)e.LocationId).FirstOrDefault()
                    : n.Type == AnnouncementType
                        ? _dbContext.Announcements.Where(a => a.AnnouncementId == n.ReferenceId).Select(a => (Guid?)a.LocationId).FirstOrDefault()
                    : n.Type == MassReminderType
                        ? _dbContext.MassSchedules.Where(s => s.ScheduleId == n.ReferenceId).Select(s => (Guid?)s.LocationId).FirstOrDefault()
                    : n.Type == FeastDayType
                        ? _dbContext.LocationFeastDays.Where(f => f.LocationFeastDayId == n.ReferenceId).Select(f => (Guid?)f.LocationId).FirstOrDefault()
                    : null,

                // NOTE: Feast days reference a location_feast_days row, which carries the calendar entry behind it.
                CalendarId = n.Type == FeastDayType
                    ? _dbContext.LocationFeastDays.Where(f => f.LocationFeastDayId == n.ReferenceId).Select(f => (Guid?)f.CalendarId).FirstOrDefault()
                    : null,

                // NOTE: The mass slot, so the service can work out which occurrence this reminder was for - the log stores no date.
                MassDayOfWeek = n.Type == MassReminderType
                    ? _dbContext.MassSchedules.Where(s => s.ScheduleId == n.ReferenceId).Select(s => (int?)s.DayOfWeek).FirstOrDefault()
                    : null,

                MassTime = n.Type == MassReminderType
                    ? _dbContext.MassSchedules.Where(s => s.ScheduleId == n.ReferenceId).Select(s => (TimeOnly?)s.MassTime).FirstOrDefault()
                    : null,

                // NOTE: Feast dates are either fixed or an annually recurring month/day; both come back and the service picks.
                FeastSpecificDate = n.Type == FeastDayType
                    ? _dbContext.LocationFeastDays.Where(f => f.LocationFeastDayId == n.ReferenceId)
                        .Select(f => f.Calendar.SpecificDate).FirstOrDefault()
                    : null,

                FeastMonth = n.Type == FeastDayType
                    ? _dbContext.LocationFeastDays.Where(f => f.LocationFeastDayId == n.ReferenceId)
                        .Select(f => f.Calendar.Month).FirstOrDefault()
                    : null,

                FeastDayOfMonth = n.Type == FeastDayType
                    ? _dbContext.LocationFeastDays.Where(f => f.LocationFeastDayId == n.ReferenceId)
                        .Select(f => f.Calendar.Day).FirstOrDefault()
                    : null
            })
            .ToListAsync(ct);
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default)
    {
        // NOTE: A COUNT over the same predicate the list uses - no rows are materialised, so this is cheap enough to poll.
        return await UserInbox(userId).CountAsync(n => !n.IsRead, ct);
    }

    public async Task<bool> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct = default)
    {
        // NOTE: Scoped to the owner so a caller cannot touch another user's row. Re-marking a read one still matches.
        var affected = await _dbContext.NotificationsLogs
            .Where(n => n.NotificationId == notificationId && n.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);

        return affected > 0;
    }

    public async Task<int> MarkAllReadAsync(Guid userId, CancellationToken ct = default)
    {
        // NOTE: Restricted to unread rows so an already-read inbox costs no writes.
        return await _dbContext.NotificationsLogs
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);
    }
}
