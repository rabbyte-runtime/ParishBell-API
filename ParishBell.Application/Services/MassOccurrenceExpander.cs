using ParishBell.Core.DTOs.Common;
using ParishBell.Core.DTOs.Mass;

namespace ParishBell.Application.Services;

// NOTE: Mass times are stored weekly but read as dated occurrences everywhere.
// NOTE: Expansion lives here so the calendar and a church profile produce identical dates.
// NOTE: It also keeps clients from re-implementing the weekday maths.
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
            // NOTE: Re-sorted because expansion interleaves schedules.
            .OrderBy(i => i.Date)
            .ThenBy(i => i.MassTime)
            .ThenBy(i => i.ScheduleId)
            .ToList();
    }

    // NOTE: Every date in the window on which this mass is celebrated.
    // NOTE: Weekly entries run the whole window; a special is clipped to its own dates.
    private static IEnumerable<DateOnly> Occurrences(MassSchedulePatternResult schedule, DateOnly fromDate, DateOnly toDate)
    {
        var start = fromDate;
        var end = toDate;

        if (schedule.IsSpecial)
        {
            // NOTE: A missing side is open-ended, so clip only where a bound exists.
            if (schedule.ValidFrom is { } validFrom && validFrom > start) start = validFrom;
            if (schedule.ValidTo is { } validTo && validTo < end) end = validTo;
        }

        if (start > end)
            yield break;

        // NOTE: DayOfWeek is 0=Sunday..6=Saturday, matching System.DayOfWeek.
        var shift = ((schedule.DayOfWeek - (int)start.DayOfWeek) + 7) % 7;

        for (var date = start.AddDays(shift); date <= end; date = date.AddDays(7))
            yield return date;
    }
}
