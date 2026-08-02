using ParishBell.Core.DTOs.Mass;
using ParishBell.Core.Interfaces;

namespace ParishBell.Application.Services;

public class MassScheduleService(IMassScheduleRepository massScheduleRepository) : IMassScheduleService
{
    private readonly IMassScheduleRepository _massScheduleRepository = massScheduleRepository;

    public async Task<MassScheduleCalendarDto> GetFollowedMassSchedulesAsync(
        Guid userId,
        string languageCode,
        int month,
        int year,
        CancellationToken ct = default)
    {
        // NOTE: The calendar shows a whole month, so we bound the query to that month's first and last day.
        var fromDate = new DateOnly(year, month, 1);
        var toDate = new DateOnly(year, month, DateTime.DaysInMonth(year, month));

        var patterns = await _massScheduleRepository.GetForFollowedLocationsAsync(userId, languageCode, fromDate, toDate, ct);

        return new MassScheduleCalendarDto
        {
            Month = month,
            Year = year,

            // NOTE: Shared with the church profile so both surfaces produce the same dates from the same rows.
            Items = MassOccurrenceExpander.Expand(patterns, fromDate, toDate)
        };
    }
}
