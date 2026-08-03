using ParishBell.Core.DTOs.Mass;

namespace ParishBell.Core.Interfaces;

public interface IMassScheduleService
{
    // NOTE: One month of mass times across every followed church, expanded onto dates.
    // NOTE: Each occurrence carries the caller reminder state.
    // NOTE: Following nothing, or a month no mass falls in, is an empty list rather than an error.
    Task<MassScheduleCalendarDto> GetFollowedMassSchedulesAsync(Guid userId, string languageCode, int month, int year, CancellationToken ct = default);
}
