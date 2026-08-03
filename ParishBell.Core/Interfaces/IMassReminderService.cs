using ParishBell.Core.DTOs.Mass;

namespace ParishBell.Core.Interfaces;

public interface IMassReminderService
{
    // NOTE: Everything the user has set, as a weekly agenda.
    // NOTE: The calendar cannot answer this without scrubbing month by month.
    // NOTE: Having none is an empty list, not an error.
    Task<UserMassReminderListDto> GetRemindersAsync(Guid userId, string languageCode, CancellationToken ct = default);

    // NOTE: Sets one reminder and returns the saved row so the client rebinds the bell.
    // NOTE: Idempotent - setting an existing one re-times it instead of failing.
    // NOTE: Throws NotFound when the mass does not exist or is no longer visible.
    Task<MassReminderDto> SetReminderAsync(Guid userId, SetMassReminderRequestDto request, CancellationToken ct = default);

    // NOTE: Cancels one reminder. Cancelling an already-off one still succeeds.
    // NOTE: Throws NotFound when it is not theirs - same as never existing.
    Task RemoveReminderAsync(Guid userId, Guid reminderId, CancellationToken ct = default);
}
