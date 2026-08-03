using Microsoft.EntityFrameworkCore;
using ParishBell.Core.DTOs.Notifications;
using ParishBell.Core.Entities;
using ParishBell.Core.Enums;
using ParishBell.Core.Interfaces;
using ParishBell.Infrastructure.Data;

namespace ParishBell.Infrastructure.Repositories;

public class MassReminderNotificationRepository(ParishBellDbContext dbContext) : IMassReminderNotificationRepository
{
    private readonly ParishBellDbContext _dbContext = dbContext;
    private const string DefaultLanguageCode = "en";
    private const short MassReminderType = (short)NotificationType.MassReminder;

    public async Task<List<DueMassReminder>> GetActiveRemindersAsync(IReadOnlyCollection<int> daysOfWeek, CancellationToken ct = default)
    {
        if (daysOfWeek.Count == 0)
            return [];

        // IMPORTANT: Every condition here is a reason a push must not go out.
        // NOTE: Cancelled reminder, removed mass, hidden church, dead account, or opted out.
        var reminders = await _dbContext.UserMassReminders
            .AsNoTracking()
            .Where(r => r.IsActive
                     && daysOfWeek.Contains(r.Schedule.DayOfWeek)
                     && r.Schedule.IsActive
                     && r.Schedule.Location.IsApproved && r.Schedule.Location.IsActive && !r.Schedule.Location.IsRejected
                     && r.User.IsActive
                     && r.User.NotifyMassReminders)
            .Select(r => new
            {
                r.UserId,
                r.MinutesBefore,
                r.ScheduleId,
                r.Schedule.LocationId,
                r.Schedule.DayOfWeek,
                r.Schedule.MassTime,
                r.Schedule.IsSpecial,
                r.Schedule.ValidFrom,
                r.Schedule.ValidTo,
                LanguageId = r.User.PreferredLanguage,
                LanguageCode = r.User.PreferredLanguageNavigation.LanguageCode
            })
            .ToListAsync(ct);

        if (reminders.Count == 0)
            return [];

        var scheduleIds = reminders.Select(r => r.ScheduleId).Distinct().ToList();
        var locationIds = reminders.Select(r => r.LocationId).Distinct().ToList();
        var languageIds = reminders.Select(r => r.LanguageId).Distinct().ToList();

        var englishId = await _dbContext.Languages
            .AsNoTracking()
            .Where(l => l.LanguageCode == DefaultLanguageCode)
            .Select(l => (Guid?)l.LanguageId)
            .FirstOrDefaultAsync(ct);

        // NOTE: Each recipient language plus English, so fallback needs no second round trip.
        var labels = await _dbContext.MassScheduleTranslations
            .AsNoTracking()
            .Where(t => scheduleIds.Contains(t.ScheduleId) && (languageIds.Contains(t.LanguageId) || t.LanguageId == englishId))
            .Select(t => new { t.ScheduleId, t.LanguageId, t.Label })
            .ToListAsync(ct);

        var names = await _dbContext.LocationTranslations
            .AsNoTracking()
            .Where(t => locationIds.Contains(t.LocationId) && (languageIds.Contains(t.LanguageId) || t.LanguageId == englishId))
            .Select(t => new { t.LocationId, t.LanguageId, t.Name })
            .ToListAsync(ct);

        return [.. reminders.Select(r => new DueMassReminder
        {
            UserId = r.UserId,
            LanguageCode = r.LanguageCode,
            MinutesBefore = r.MinutesBefore,
            ScheduleId = r.ScheduleId,
            LocationId = r.LocationId,
            DayOfWeek = r.DayOfWeek,
            MassTime = r.MassTime,
            IsSpecial = r.IsSpecial,
            ValidFrom = r.ValidFrom,
            ValidTo = r.ValidTo,

            Label = labels.FirstOrDefault(t => t.ScheduleId == r.ScheduleId && t.LanguageId == r.LanguageId)?.Label
                 ?? labels.FirstOrDefault(t => t.ScheduleId == r.ScheduleId && t.LanguageId == englishId)?.Label
                 ?? string.Empty,

            LocationName = names.FirstOrDefault(t => t.LocationId == r.LocationId && t.LanguageId == r.LanguageId)?.Name
                        ?? names.FirstOrDefault(t => t.LocationId == r.LocationId && t.LanguageId == englishId)?.Name
                        ?? string.Empty
        })];
    }

    public async Task<HashSet<(Guid UserId, Guid ScheduleId, DateOnly OccurrenceDate)>> GetAlreadyNotifiedAsync(
        DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        // NOTE: The dedup key. Without occurrence_date a weekly reminder would look identical week after week.
        var rows = await _dbContext.NotificationsLogs
            .AsNoTracking()
            .Where(n => n.Type == MassReminderType
                     && n.ReferenceId != null
                     && n.OccurrenceDate != null
                     && n.OccurrenceDate >= fromDate && n.OccurrenceDate <= toDate)
            .Select(n => new { n.UserId, ScheduleId = n.ReferenceId!.Value, OccurrenceDate = n.OccurrenceDate!.Value })
            .ToListAsync(ct);

        return [.. rows.Select(r => (r.UserId, r.ScheduleId, r.OccurrenceDate))];
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
            .Where(n => n.Type == MassReminderType && !n.IsSent)
            .OrderBy(n => n.NotificationId)
            .Take(batchSize)
            .Select(n => new PendingNotification
            {
                NotificationId = n.NotificationId,
                UserId = n.UserId,
                Title = n.Title,
                Body = n.Body,
                ReferenceId = n.ReferenceId,

                // NOTE: reference_id is the schedule, so the church comes from the schedule row.
                LocationId = _dbContext.MassSchedules
                    .Where(s => s.ScheduleId == n.ReferenceId)
                    .Select(s => (Guid?)s.LocationId)
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
