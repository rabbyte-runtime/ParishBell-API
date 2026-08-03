using ParishBell.Core.DTOs.Common;

namespace ParishBell.Core.Interfaces;

public interface IMassReminderRepository
{
    // NOTE: Every reminder the user has set, with the mass and church behind it, ordered as a weekly agenda.
    // NOTE: Reminders on a hidden mass or church are left out - they can never fire.
    // NOTE: Unfollowed churches stay in the list, flagged.
    Task<List<UserMassReminderResult>> GetForUserAsync(Guid userId, string languageCode, CancellationToken ct = default);

    // NOTE: Whether the mass exists and is visible - a hidden one cannot be remembered.
    Task<bool> IsScheduleRemindableAsync(Guid scheduleId, CancellationToken ct = default);

    // NOTE: Creates the reminder, or updates the one already there.
    // NOTE: uq_user_schedule allows only one per user and mass.
    // NOTE: Re-saving switches a previously switched-off reminder back on.
    Task<MassReminderResult> UpsertAsync(Guid userId, Guid scheduleId, int minutesBefore, CancellationToken ct = default);

    // NOTE: Switches off every reminder the user holds on masses at one church. Returns how many were still on.
    // NOTE: Used on unfollow - a push from a church they left is the surprise this prevents.
    Task<int> DisableForLocationAsync(Guid userId, Guid locationId, CancellationToken ct = default);

    // NOTE: Switches the reminder off. False when missing or not theirs.
    // NOTE: Idempotent - cancelling an already-off reminder still matches the row.
    Task<bool> DisableAsync(Guid userId, Guid reminderId, CancellationToken ct = default);
}
