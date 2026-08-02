using ParishBell.Core.DTOs.Mass;

namespace ParishBell.Core.Interfaces;

public interface IMassScheduleService
{
    // NOTE: One month of the signed-in user's mass times - every followed church, expanded onto real dates, with their reminder state on each.
    // NOTE: Following nothing, or a month no mass falls in, is an empty list rather than an error.
    Task<MassScheduleCalendarDto> GetFollowedMassSchedulesAsync(Guid userId, string languageCode, int month, int year, CancellationToken ct = default);
}
