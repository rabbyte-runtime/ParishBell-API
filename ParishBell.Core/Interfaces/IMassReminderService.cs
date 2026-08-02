using ParishBell.Core.DTOs.Mass;

namespace ParishBell.Core.Interfaces;

public interface IMassReminderService
{
    // NOTE: Sets the signed-in user's reminder for one mass, returning the saved row so the client can rebind the bell.
    // NOTE: Idempotent - setting one that already exists updates the timing rather than failing on the unique index.
    // NOTE: Throws NotFound when the mass does not exist or is no longer visible.
    // NOTE: Everything the user has set, as a weekly agenda - the standalone answer to "what reminders do I have?", which the calendar cannot give without scrubbing months.
    // NOTE: Having none is an empty list, not an error.
    Task<UserMassReminderListDto> GetRemindersAsync(Guid userId, string languageCode, CancellationToken ct = default);

    Task<MassReminderDto> SetReminderAsync(Guid userId, SetMassReminderRequestDto request, CancellationToken ct = default);

    // NOTE: Cancels one of the user's reminders. Idempotent - cancelling one that is already off succeeds.
    // NOTE: Throws NotFound when the reminder isn't theirs, which is indistinguishable from it never existing.
    Task RemoveReminderAsync(Guid userId, Guid reminderId, CancellationToken ct = default);
}
