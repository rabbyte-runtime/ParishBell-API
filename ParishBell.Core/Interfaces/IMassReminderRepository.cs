using ParishBell.Core.DTOs.Common;

namespace ParishBell.Core.Interfaces;

public interface IMassReminderRepository
{
    // NOTE: Every reminder the user has set, with the mass and church behind it, ordered as a weekly agenda.
    // NOTE: Reminders whose mass or church has since been hidden are left out - they can never fire again. Unfollowed churches stay, flagged.
    Task<List<UserMassReminderResult>> GetForUserAsync(Guid userId, string languageCode, CancellationToken ct = default);

    // NOTE: Whether the mass exists and is still visible - an inactive schedule, or one at a church that is hidden, cannot be remembered.
    Task<bool> IsScheduleRemindableAsync(Guid scheduleId, CancellationToken ct = default);

    // NOTE: Creates the user's reminder for that mass, or updates the one already there. uq_user_schedule allows only one per user and mass.
    // NOTE: Re-saving switches a previously switched-off reminder back on.
    Task<MassReminderResult> UpsertAsync(Guid userId, Guid scheduleId, int minutesBefore, CancellationToken ct = default);

    // NOTE: Switches off every reminder the user holds on masses at one church. Returns how many were still on.
    // NOTE: Used when they unfollow it - a push from a church they walked away from is the surprise this prevents.
    Task<int> DisableForLocationAsync(Guid userId, Guid locationId, CancellationToken ct = default);

    // NOTE: Switches the reminder off so it stops firing. False when it does not exist or belongs to someone else.
    // NOTE: Idempotent - cancelling an already-off reminder still matches, since the row is what is being addressed.
    Task<bool> DisableAsync(Guid userId, Guid reminderId, CancellationToken ct = default);
}
