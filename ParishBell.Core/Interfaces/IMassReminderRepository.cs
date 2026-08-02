using ParishBell.Core.DTOs.Common;

namespace ParishBell.Core.Interfaces;

public interface IMassReminderRepository
{
    // NOTE: Whether the mass exists and is still visible - an inactive schedule, or one at a church that is hidden, cannot be remembered.
    Task<bool> IsScheduleRemindableAsync(Guid scheduleId, CancellationToken ct = default);

    // NOTE: Creates the user's reminder for that mass, or updates the one already there. uq_user_schedule allows only one per user and mass.
    // NOTE: Re-saving switches a previously switched-off reminder back on.
    Task<MassReminderResult> UpsertAsync(Guid userId, Guid scheduleId, int minutesBefore, CancellationToken ct = default);
}
