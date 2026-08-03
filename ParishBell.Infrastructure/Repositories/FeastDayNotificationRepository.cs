using Microsoft.EntityFrameworkCore;
using ParishBell.Core.DTOs.Notifications;
using ParishBell.Core.Entities;
using ParishBell.Core.Enums;
using ParishBell.Core.Interfaces;
using ParishBell.Infrastructure.Data;

namespace ParishBell.Infrastructure.Repositories;

public class FeastDayNotificationRepository(ParishBellDbContext dbContext) : IFeastDayNotificationRepository
{
    private readonly ParishBellDbContext _dbContext = dbContext;
    private const short FeastDayType = (short)NotificationType.FeastDay;

    public async Task<List<DueFeastDay>> GetPinnedFeastDaysAsync(CancellationToken ct = default)
    {
        // NOTE: Only feasts a live church has pinned - a global calendar entry nobody pinned has no audience here.
        return await _dbContext.LocationFeastDays
            .AsNoTracking()
            .Where(f => f.Location.IsApproved && f.Location.IsActive && !f.Location.IsRejected)
            .Select(f => new DueFeastDay
            {
                LocationFeastDayId = f.LocationFeastDayId,
                CalendarId = f.CalendarId,
                LocationId = f.LocationId,
                IsRecurringAnnually = f.Calendar.IsRecurringAnnually,
                Month = f.Calendar.Month,
                Day = f.Calendar.Day,
                SpecificDate = f.Calendar.SpecificDate
            })
            .ToListAsync(ct);
    }

    public async Task<List<FeastDayRecipient>> GetRecipientsWithoutLogAsync(
        Guid locationFeastDayId, Guid locationId, DateOnly occurrenceDate, CancellationToken ct = default)
    {
        // NOTE: Anti-join on (user, feast, date) so re-polling the same day queues nothing twice.
        // IMPORTANT: The occurrence date is part of the key - an annually recurring feast is the same reference_id every year.
        return await _dbContext.UserFollowedLocations
            .AsNoTracking()
            .Where(f => f.LocationId == locationId
                     && f.User.IsActive
                     && f.User.NotifyFeastDays
                     && !_dbContext.NotificationsLogs.Any(n => n.UserId == f.UserId
                                                            && n.Type == FeastDayType
                                                            && n.ReferenceId == locationFeastDayId
                                                            && n.OccurrenceDate == occurrenceDate))
            .Select(f => new FeastDayRecipient(f.UserId, f.User.PreferredLanguage, f.User.PreferredLanguageNavigation.LanguageCode))
            .ToListAsync(ct);
    }

    public async Task<Dictionary<Guid, (string Title, string? Description)>> GetTranslationsAsync(Guid calendarId, CancellationToken ct = default)
    {
        var rows = await _dbContext.LiturgicalCalendarTranslations
            .AsNoTracking()
            .Where(t => t.CalendarId == calendarId)
            .Select(t => new { t.LanguageId, t.Title, t.Description })
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.LanguageId, r => (r.Title, r.Description));
    }

    public async Task AddLogsAsync(IReadOnlyCollection<NotificationsLog> logs, CancellationToken ct = default)
    {
        if (logs.Count == 0) return;

        _dbContext.NotificationsLogs.AddRange(logs);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<List<PendingNotification>> GetPendingAsync(int batchSize, CancellationToken ct = default)
    {
        return await _dbContext.NotificationsLogs
            .AsNoTracking()
            .Where(n => n.Type == FeastDayType && !n.IsSent)
            .OrderBy(n => n.NotificationId)
            .Take(batchSize)
            .Select(n => new PendingNotification
            {
                NotificationId = n.NotificationId,
                UserId = n.UserId,
                Title = n.Title,
                Body = n.Body,
                ReferenceId = n.ReferenceId,

                // NOTE: reference_id is the location_feast_days row, which carries both the church and the calendar entry.
                LocationId = _dbContext.LocationFeastDays
                    .Where(f => f.LocationFeastDayId == n.ReferenceId)
                    .Select(f => (Guid?)f.LocationId)
                    .FirstOrDefault(),

                CalendarId = _dbContext.LocationFeastDays
                    .Where(f => f.LocationFeastDayId == n.ReferenceId)
                    .Select(f => (Guid?)f.CalendarId)
                    .FirstOrDefault(),

                OccurrenceDate = n.OccurrenceDate
            })
            .ToListAsync(ct);
    }

    public async Task MarkSentAsync(IReadOnlyCollection<Guid> notificationIds, DateTime sentAt, CancellationToken ct = default)
    {
        if (notificationIds.Count == 0) return;

        await _dbContext.NotificationsLogs
            .Where(n => notificationIds.Contains(n.NotificationId))
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.IsSent, true)
                .SetProperty(n => n.SentAt, sentAt), ct);
    }
}
