using ParishBell.Core.DTOs.Common;

namespace ParishBell.Core.Interfaces;

public interface IMassScheduleRepository
{
    // NOTE: The weekly patterns behind the user's mass timetable, across every location they follow, each carrying that user's own reminder.
    // NOTE: Returns patterns, not dates - a row is included when it could fall inside [fromDate, toDate]; the service expands it onto real days.
    Task<List<MassSchedulePatternResult>> GetForFollowedLocationsAsync(Guid userId, string languageCode, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);
}
