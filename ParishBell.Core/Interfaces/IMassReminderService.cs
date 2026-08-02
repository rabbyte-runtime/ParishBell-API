using ParishBell.Core.DTOs.Mass;

namespace ParishBell.Core.Interfaces;

public interface IMassReminderService
{
    // NOTE: Sets the signed-in user's reminder for one mass, returning the saved row so the client can rebind the bell.
    // NOTE: Idempotent - setting one that already exists updates the timing rather than failing on the unique index.
    // NOTE: Throws NotFound when the mass does not exist or is no longer visible.
    Task<MassReminderDto> SetReminderAsync(Guid userId, SetMassReminderRequestDto request, CancellationToken ct = default);
}
