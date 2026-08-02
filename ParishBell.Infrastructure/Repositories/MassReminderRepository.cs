using Microsoft.EntityFrameworkCore;
using ParishBell.Core.DTOs.Common;
using ParishBell.Core.Entities;
using ParishBell.Core.Interfaces;
using ParishBell.Infrastructure.Data;

namespace ParishBell.Infrastructure.Repositories;

public class MassReminderRepository(ParishBellDbContext dbContext) : IMassReminderRepository
{
    private readonly ParishBellDbContext _dbContext = dbContext;
    private const string DefaultLanguageCode = "en";

    public async Task<List<UserMassReminderResult>> GetForUserAsync(Guid userId, string languageCode, CancellationToken ct = default)
    {
        // NOTE: Resolve the requested language + English to their UUIDs
        var langs = await _dbContext.Languages
            .AsNoTracking()
            .Where(l => l.LanguageCode == languageCode || l.LanguageCode == DefaultLanguageCode)
            .Select(l => new { l.LanguageId, l.LanguageCode })
            .ToListAsync(ct);

        var englishId = langs.FirstOrDefault(l => l.LanguageCode == DefaultLanguageCode)?.LanguageId;
        var requestedId = langs.FirstOrDefault(l => l.LanguageCode == languageCode)?.LanguageId ?? englishId;

        // NOTE: A reminder on a hidden mass or church can never fire and has nothing to render, so it is dropped rather than listed.
        // NOTE: Ordered as a weekly agenda - day, then time, then schedule for a stable order across churches sharing a slot.
        var reminders = await _dbContext.UserMassReminders
            .AsNoTracking()
            .Where(r => r.UserId == userId && r.Schedule.IsActive
                     && r.Schedule.Location.IsApproved && r.Schedule.Location.IsActive && !r.Schedule.Location.IsRejected)
            .OrderBy(r => r.Schedule.DayOfWeek)
            .ThenBy(r => r.Schedule.MassTime)
            .ThenBy(r => r.ScheduleId)
            .Select(r => new
            {
                r.ReminderId,
                r.MinutesBefore,
                r.IsActive,
                r.ScheduleId,
                r.Schedule.LocationId,
                r.Schedule.DayOfWeek,
                r.Schedule.MassTime,
                r.Schedule.IsSpecial,
                r.Schedule.ValidFrom,
                r.Schedule.ValidTo
            })
            .ToListAsync(ct);

        if (reminders.Count == 0)
            return [];

        var scheduleIds = reminders.Select(r => r.ScheduleId).ToList();
        var locationIds = reminders.Select(r => r.LocationId).Distinct().ToList();

        var translations = await _dbContext.MassScheduleTranslations
            .AsNoTracking()
            .Where(t => scheduleIds.Contains(t.ScheduleId) && (t.LanguageId == requestedId || t.LanguageId == englishId))
            .Select(t => new { t.ScheduleId, t.LanguageId, t.Label })
            .ToListAsync(ct);

        var locationNames = await _dbContext.LocationTranslations
            .AsNoTracking()
            .Where(t => locationIds.Contains(t.LocationId) && (t.LanguageId == requestedId || t.LanguageId == englishId))
            .Select(t => new { t.LocationId, t.LanguageId, t.Name })
            .ToListAsync(ct);

        // NOTE: Follow state is not part of the reminder - it is looked up so the list can flag ones the user has walked away from.
        var followedLocationIds = await _dbContext.UserFollowedLocations
            .AsNoTracking()
            .Where(f => f.UserId == userId && locationIds.Contains(f.LocationId))
            .Select(f => f.LocationId)
            .ToListAsync(ct);

        var result = new List<UserMassReminderResult>(reminders.Count);
        foreach (var r in reminders)
        {
            var label = translations.FirstOrDefault(t => t.ScheduleId == r.ScheduleId && t.LanguageId == requestedId)?.Label
                     ?? translations.FirstOrDefault(t => t.ScheduleId == r.ScheduleId && t.LanguageId == englishId)?.Label
                     ?? string.Empty;

            var locationName = locationNames.FirstOrDefault(t => t.LocationId == r.LocationId && t.LanguageId == requestedId)?.Name
                            ?? locationNames.FirstOrDefault(t => t.LocationId == r.LocationId && t.LanguageId == englishId)?.Name
                            ?? string.Empty;

            result.Add(new UserMassReminderResult(
                r.ReminderId,
                r.MinutesBefore,
                r.IsActive,
                r.ScheduleId,
                r.LocationId,
                locationName,
                r.DayOfWeek,
                r.MassTime,
                label,
                r.IsSpecial,
                r.ValidFrom,
                r.ValidTo,
                followedLocationIds.Contains(r.LocationId)));
        }

        return result;
    }

    public async Task<bool> IsScheduleRemindableAsync(Guid scheduleId, CancellationToken ct = default)
    {
        // NOTE: The church has to be live too - a mass at a rejected or deactivated location is not something to be reminded about.
        return await _dbContext.MassSchedules
            .AsNoTracking()
            .AnyAsync(s => s.ScheduleId == scheduleId && s.IsActive
                        && s.Location.IsApproved && s.Location.IsActive && !s.Location.IsRejected, ct);
    }

    public async Task<MassReminderResult> UpsertAsync(Guid userId, Guid scheduleId, int minutesBefore, CancellationToken ct = default)
    {
        // NOTE: uq_user_schedule permits one reminder per user and mass, so a second save edits the first.
        var existing = await _dbContext.UserMassReminders
            .FirstOrDefaultAsync(r => r.UserId == userId && r.ScheduleId == scheduleId, ct);

        if (existing is not null)
        {
            existing.MinutesBefore = minutesBefore;

            // NOTE: Setting a reminder again is how the user turns a switched-off one back on.
            existing.IsActive = true;
            await _dbContext.SaveChangesAsync(ct);

            return new MassReminderResult(existing.ReminderId, existing.MinutesBefore, existing.IsActive);
        }

        var reminder = new UserMassReminder
        {
            UserId = userId,
            ScheduleId = scheduleId,
            MinutesBefore = minutesBefore,
            IsActive = true
        };

        _dbContext.UserMassReminders.Add(reminder);

        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // NOTE: A concurrent request inserted the same (user, schedule) between the check and the save.
            //       Drop our failed insert, then update the row that won the race so the end state is ours.
            _dbContext.Entry(reminder).State = EntityState.Detached;

            var raced = await _dbContext.UserMassReminders
                .FirstOrDefaultAsync(r => r.UserId == userId && r.ScheduleId == scheduleId, ct);

            if (raced is null) throw;

            raced.MinutesBefore = minutesBefore;
            raced.IsActive = true;
            await _dbContext.SaveChangesAsync(ct);

            return new MassReminderResult(raced.ReminderId, raced.MinutesBefore, raced.IsActive);
        }

        return new MassReminderResult(reminder.ReminderId, reminder.MinutesBefore, reminder.IsActive);
    }

    public async Task<int> DisableForLocationAsync(Guid userId, Guid locationId, CancellationToken ct = default)
    {
        // NOTE: Cancelled rather than deleted, matching DELETE - re-following and re-enabling keeps the original timing.
        // NOTE: Restricted to still-active rows so an unfollow of a church with nothing set costs no writes.
        return await _dbContext.UserMassReminders
            .Where(r => r.UserId == userId && r.IsActive && r.Schedule.LocationId == locationId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct);
    }

    public async Task<bool> DisableAsync(Guid userId, Guid reminderId, CancellationToken ct = default)
    {
        // NOTE: Scoped to the owner so a caller cannot switch off another user's reminder. Re-cancelling an off one still matches.
        // IMPORTANT: The row is kept rather than deleted - uq_user_schedule means re-setting the reminder reuses it, and the
        //            schedule endpoint reports it as isActive:false so the bell can render off rather than unset.
        var affected = await _dbContext.UserMassReminders
            .Where(r => r.ReminderId == reminderId && r.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct);

        return affected > 0;
    }
}
