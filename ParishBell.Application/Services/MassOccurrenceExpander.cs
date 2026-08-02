using ParishBell.Core.DTOs.Common;
using ParishBell.Core.DTOs.Mass;

namespace ParishBell.Application.Services;

// NOTE: Mass times are stored as a weekly pattern but read as dated occurrences everywhere - the calendar across
// NOTE:  followed churches and a single church's profile. Expansion lives here so both get identical dates, and so
// NOTE:  no client has to re-implement the weekday maths.
public static class MassOccurrenceExpander
{
    public static List<MassOccurrenceDto> Expand(IEnumerable<MassSchedulePatternResult> patterns, DateOnly fromDate, DateOnly toDate)
    {
        return patterns
            .SelectMany(p => Occurrences(p, fromDate, toDate).Select(date => new MassOccurrenceDto
            {
                ScheduleId = p.ScheduleId,
                LocationId = p.LocationId,
                LocationName = p.LocationName,
                Date = date.ToString("yyyy-MM-dd"),
                MassTime = p.MassTime.ToString("HH:mm"),
                Label = p.Label,
                IsSpecial = p.IsSpecial,

                // NOTE: The reminder belongs to the schedule, so it repeats on every occurrence of that mass.
                Reminder = p.Reminder is null ? null : new MassReminderDto
                {
                    ReminderId = p.Reminder.ReminderId,
                    MinutesBefore = p.Reminder.MinutesBefore,
                    IsActive = p.Reminder.IsActive
                }
            }))
            // NOTE: Re-sorted because expansion interleaves schedules; a day timeline reads straight down this list.
            .OrderBy(i => i.Date)
            .ThenBy(i => i.MassTime)
            .ThenBy(i => i.ScheduleId)
            .ToList();
    }

    // NOTE: Every date in the window on which this mass is celebrated. Weekly entries run the whole window; a special
    // NOTE:  is clipped to its own valid_from/valid_to, so one that only partly overlaps yields just the days inside it.
    private static IEnumerable<DateOnly> Occurrences(MassSchedulePatternResult schedule, DateOnly fromDate, DateOnly toDate)
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
