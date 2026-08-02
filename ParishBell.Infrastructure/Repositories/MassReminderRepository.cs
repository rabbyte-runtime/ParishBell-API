using Microsoft.EntityFrameworkCore;
using ParishBell.Core.DTOs.Common;
using ParishBell.Core.Entities;
using ParishBell.Core.Interfaces;
using ParishBell.Infrastructure.Data;

namespace ParishBell.Infrastructure.Repositories;

public class MassReminderRepository(ParishBellDbContext dbContext) : IMassReminderRepository
{
    private readonly ParishBellDbContext _dbContext = dbContext;

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
