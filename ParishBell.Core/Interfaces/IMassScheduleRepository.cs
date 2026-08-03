using ParishBell.Core.DTOs.Common;

namespace ParishBell.Core.Interfaces;

public interface IMassScheduleRepository
{
    // NOTE: The weekly patterns behind the timetable, across every followed location.
    // NOTE: Each carries that user own reminder.
    // NOTE: Returns patterns, not dates - the service expands them onto real days.
    Task<List<MassSchedulePatternResult>> GetForFollowedLocationsAsync(Guid userId, string languageCode, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);
}
