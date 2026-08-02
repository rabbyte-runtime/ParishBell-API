using ParishBell.Core.DTOs.Common;
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

        var results = await _massScheduleRepository.GetForFollowedLocationsAsync(userId, languageCode, fromDate, toDate, ct);

        // NOTE: Events and feast days arrive already dated; mass times arrive as a weekly pattern, so they are expanded
        // NOTE:  here into one entry per day they actually fall on. That keeps all three calendar sources the same shape.
        var items = results
            .SelectMany(r => Occurrences(r, fromDate, toDate).Select(date => new MassOccurrenceDto
            {
                ScheduleId = r.ScheduleId,
                LocationId = r.LocationId,
                LocationName = r.LocationName,
                Date = date.ToString("yyyy-MM-dd"),
                MassTime = r.MassTime.ToString("HH:mm"),
                Label = r.Label,
                IsSpecial = r.IsSpecial,

                // NOTE: The reminder belongs to the schedule, so it repeats on every occurrence of that mass.
                Reminder = r.Reminder is null ? null : new MassReminderDto
                {
                    ReminderId = r.Reminder.ReminderId,
                    MinutesBefore = r.Reminder.MinutesBefore,
                    IsActive = r.Reminder.IsActive
                }
            }))
            // NOTE: Re-sorted because expansion interleaves schedules; the day timeline reads straight down this list.
            .OrderBy(i => i.Date)
            .ThenBy(i => i.MassTime)
            .ThenBy(i => i.ScheduleId)
            .ToList();

        return new MassScheduleCalendarDto
        {
            Month = month,
            Year = year,
            Items = items
        };
    }

    // NOTE: Every date in the window on which this mass is celebrated. Weekly entries run the whole month; a special
    // NOTE:  is clipped to its own valid_from/valid_to, so one that only partly overlaps yields just the days inside it.
    private static IEnumerable<DateOnly> Occurrences(FollowedMassScheduleResult schedule, DateOnly fromDate, DateOnly toDate)
    {
        var start = fromDate;
        var end = toDate;

        if (schedule.IsSpecial)
        {
            // NOTE: A missing side of the window is open-ended, not empty - clip only where a bound actually exists.
            if (schedule.ValidFrom is { } validFrom && validFrom > start) start = validFrom;
            if (schedule.ValidTo is { } validTo && validTo < end) end = validTo;
        }

        if (start > end)
            yield break;

        // NOTE: DayOfWeek is 0=Sunday..6=Saturday in the DB, which is exactly System.DayOfWeek's own numbering.
        var shift = ((schedule.DayOfWeek - (int)start.DayOfWeek) + 7) % 7;

        for (var date = start.AddDays(shift); date <= end; date = date.AddDays(7))
            yield return date;
    }
}
