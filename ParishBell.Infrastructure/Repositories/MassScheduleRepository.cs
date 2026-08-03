using Microsoft.EntityFrameworkCore;
using ParishBell.Core.DTOs.Common;
using ParishBell.Core.Interfaces;
using ParishBell.Infrastructure.Data;

namespace ParishBell.Infrastructure.Repositories;

public class MassScheduleRepository(ParishBellDbContext dbContext) : IMassScheduleRepository
{
    private readonly ParishBellDbContext _dbContext = dbContext;
    private const string DefaultLanguageCode = "en";

    public async Task<List<MassSchedulePatternResult>> GetForFollowedLocationsAsync(
        Guid userId,
        string languageCode,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct = default)
    {
        // NOTE: Resolve the requested language + English to their UUIDs
        var langs = await _dbContext.Languages
            .AsNoTracking()
            .Where(l => l.LanguageCode == languageCode || l.LanguageCode == DefaultLanguageCode)
            .Select(l => new { l.LanguageId, l.LanguageCode })
            .ToListAsync(ct);

        var englishId = langs.FirstOrDefault(l => l.LanguageCode == DefaultLanguageCode)?.LanguageId;
        var requestedId = langs.FirstOrDefault(l => l.LanguageCode == languageCode)?.LanguageId ?? englishId;

        // NOTE: Locations the user follows that are still live — idx_ufl_user covers the user filter
        var followedLocationIds = await _dbContext.UserFollowedLocations
            .AsNoTracking()
            .Where(f => f.UserId == userId && f.Location.IsApproved && f.Location.IsActive && !f.Location.IsRejected)
            .Select(f => f.LocationId)
            .ToListAsync(ct);

        if (followedLocationIds.Count == 0)
            return [];

        // NOTE: Weekly entries always apply.
        // NOTE: A special is kept only when its window overlaps the requested month.
        // NOTE: An open-ended side counts as reaching that far.
        // NOTE: Ordered by day then time, then ScheduleId for a stable order across churches sharing a slot.
        var schedules = await _dbContext.MassSchedules
            .AsNoTracking()
            .Where(s => followedLocationIds.Contains(s.LocationId) && s.IsActive
                     && (!s.IsSpecial
                         || ((s.ValidFrom == null || s.ValidFrom <= toDate)
                          && (s.ValidTo == null || s.ValidTo >= fromDate))))
            .OrderBy(s => s.DayOfWeek)
            .ThenBy(s => s.MassTime)
            .ThenBy(s => s.ScheduleId)
            .Select(s => new { s.ScheduleId, s.LocationId, s.DayOfWeek, s.MassTime, s.IsSpecial, s.ValidFrom, s.ValidTo })
            .ToListAsync(ct);

        if (schedules.Count == 0)
            return [];

        var scheduleIds = schedules.Select(s => s.ScheduleId).ToList();
        var scheduleLocationIds = schedules.Select(s => s.LocationId).Distinct().ToList();

        var translations = await _dbContext.MassScheduleTranslations
            .AsNoTracking()
            .Where(t => scheduleIds.Contains(t.ScheduleId) && (t.LanguageId == requestedId || t.LanguageId == englishId))
            .Select(t => new { t.ScheduleId, t.LanguageId, t.Label })
            .ToListAsync(ct);

        // NOTE: Location names for the badge on each entry — requested language with English fallback
        var locationNames = await _dbContext.LocationTranslations
            .AsNoTracking()
            .Where(t => scheduleLocationIds.Contains(t.LocationId) && (t.LanguageId == requestedId || t.LanguageId == englishId))
            .Select(t => new { t.LocationId, t.LanguageId, t.Name })
            .ToListAsync(ct);

        // IMPORTANT: Scoped to the caller - another user row must never reach this list.
        // NOTE: Switched-off reminders are fetched too, so the toggle can render off.
        var reminders = await _dbContext.UserMassReminders
            .AsNoTracking()
            .Where(r => r.UserId == userId && scheduleIds.Contains(r.ScheduleId))
            .Select(r => new { r.ReminderId, r.ScheduleId, r.MinutesBefore, r.IsActive })
            .ToListAsync(ct);

        var result = new List<MassSchedulePatternResult>(schedules.Count);
        foreach (var s in schedules)
        {
            var label = translations.FirstOrDefault(t => t.ScheduleId == s.ScheduleId && t.LanguageId == requestedId)?.Label
                     ?? translations.FirstOrDefault(t => t.ScheduleId == s.ScheduleId && t.LanguageId == englishId)?.Label
                     ?? string.Empty;

            var locationName = locationNames.FirstOrDefault(t => t.LocationId == s.LocationId && t.LanguageId == requestedId)?.Name
                            ?? locationNames.FirstOrDefault(t => t.LocationId == s.LocationId && t.LanguageId == englishId)?.Name
                            ?? string.Empty;

            // NOTE: uq_user_schedule makes this at most one row per user and mass.
            var reminder = reminders.FirstOrDefault(r => r.ScheduleId == s.ScheduleId);

            result.Add(new MassSchedulePatternResult(
                s.ScheduleId,
                s.LocationId,
                locationName,
                s.DayOfWeek,
                s.MassTime,
                label,
                s.IsSpecial,
                s.ValidFrom,
                s.ValidTo,
                reminder is null ? null : new MassReminderResult(reminder.ReminderId, reminder.MinutesBefore, reminder.IsActive)));
        }

        return result;
    }
}
